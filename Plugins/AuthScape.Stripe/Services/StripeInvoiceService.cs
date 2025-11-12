using AuthScape.Models.PaymentGateway.Stripe;
using Models.AppSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;
using Stripe;
using System.Text.Json;

namespace AuthScape.StripePayment.Services
{
    public interface IStripeInvoiceService
    {
        Task<InvoiceResult> GetInvoiceAsync(Guid invoiceId);
        Task<InvoiceResult> GetInvoiceByStripeIdAsync(string stripeInvoiceId);
        Task<List<InvoiceResult>> ListInvoicesAsync(Guid walletId, InvoiceFilter filter = null);
        Task<InvoiceResult> RetryPaymentAsync(Guid invoiceId);
        Task<byte[]> DownloadInvoicePdfAsync(Guid invoiceId);
        Task<string> GetInvoicePdfUrlAsync(Guid invoiceId);
        Task<StripeInvoice> SyncInvoiceFromStripeAsync(string stripeInvoiceId);
        Task SyncAllInvoicesForWalletAsync(Guid walletId);
    }

    public class StripeInvoiceService : IStripeInvoiceService
    {
        readonly DatabaseContext context;
        readonly AppSettings appSettings;

        public StripeInvoiceService(IOptions<AppSettings> appSettings, DatabaseContext context)
        {
            this.appSettings = appSettings.Value;
            this.context = context;

            if (this.appSettings.Stripe != null && this.appSettings.Stripe.SecretKey != null)
            {
                StripeConfiguration.ApiKey = this.appSettings.Stripe.SecretKey;
            }
        }

        public async Task<InvoiceResult> GetInvoiceAsync(Guid invoiceId)
        {
            var invoice = await context.StripeInvoices
                .Include(i => i.LineItems)
                .Include(i => i.Wallet)
                .Include(i => i.Subscription)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                return new InvoiceResult { Success = false, Error = "Invoice not found" };

            return new InvoiceResult { Success = true, Invoice = invoice };
        }

        public async Task<InvoiceResult> GetInvoiceByStripeIdAsync(string stripeInvoiceId)
        {
            var invoice = await context.StripeInvoices
                .Include(i => i.LineItems)
                .Include(i => i.Wallet)
                .Include(i => i.Subscription)
                .FirstOrDefaultAsync(i => i.StripeInvoiceId == stripeInvoiceId);

            if (invoice == null)
                return new InvoiceResult { Success = false, Error = "Invoice not found" };

            return new InvoiceResult { Success = true, Invoice = invoice };
        }

        public async Task<List<InvoiceResult>> ListInvoicesAsync(Guid walletId, InvoiceFilter filter = null)
        {
            var query = context.StripeInvoices
                .Include(i => i.LineItems)
                .Include(i => i.Subscription)
                .Where(i => i.WalletId == walletId);

            if (filter != null)
            {
                if (filter.Status.HasValue)
                    query = query.Where(i => i.Status == filter.Status.Value);

                if (filter.StartDate.HasValue)
                    query = query.Where(i => i.CreatedAt >= filter.StartDate.Value);

                if (filter.EndDate.HasValue)
                    query = query.Where(i => i.CreatedAt <= filter.EndDate.Value);

                if (filter.SubscriptionId.HasValue)
                    query = query.Where(i => i.SubscriptionId == filter.SubscriptionId.Value);
            }

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Take(filter?.Limit ?? 100)
                .ToListAsync();

            return invoices.Select(i => new InvoiceResult
            {
                Success = true,
                Invoice = i
            }).ToList();
        }

        public async Task<InvoiceResult> RetryPaymentAsync(Guid invoiceId)
        {
            try
            {
                var invoice = await context.StripeInvoices
                    .FirstOrDefaultAsync(i => i.Id == invoiceId);

                if (invoice == null)
                    return new InvoiceResult { Success = false, Error = "Invoice not found" };

                if (invoice.Status != StripeInvoiceStatus.Open)
                    return new InvoiceResult { Success = false, Error = "Invoice is not open for payment" };

                var features = appSettings.Subscriptions ?? new SubscriptionFeatures();
                if (!features.AutoRetryFailedPayments)
                    return new InvoiceResult { Success = false, Error = "Payment retry not enabled" };

                var service = new InvoiceService();
                var paidInvoice = await service.PayAsync(invoice.StripeInvoiceId);

                await SyncInvoiceFromStripeAsync(invoice.StripeInvoiceId);

                return new InvoiceResult { Success = true, Invoice = invoice };
            }
            catch (StripeException ex)
            {
                return new InvoiceResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<byte[]> DownloadInvoicePdfAsync(Guid invoiceId)
        {
            try
            {
                var invoice = await context.StripeInvoices
                    .FirstOrDefaultAsync(i => i.Id == invoiceId);

                if (invoice == null)
                    throw new Exception("Invoice not found");

                var features = appSettings.Subscriptions ?? new SubscriptionFeatures();
                if (!features.AllowInvoiceDownload)
                    throw new Exception("Invoice download not enabled");

                if (string.IsNullOrEmpty(invoice.InvoicePdfUrl))
                {
                    // Fetch latest invoice data from Stripe
                    var service = new InvoiceService();
                    var stripeInvoice = await service.GetAsync(invoice.StripeInvoiceId);
                    invoice.InvoicePdfUrl = stripeInvoice.InvoicePdf;
                    await context.SaveChangesAsync();
                }

                if (string.IsNullOrEmpty(invoice.InvoicePdfUrl))
                    throw new Exception("Invoice PDF not available");

                // Download PDF from Stripe URL
                using var httpClient = new HttpClient();
                var pdfBytes = await httpClient.GetByteArrayAsync(invoice.InvoicePdfUrl);
                return pdfBytes;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download invoice PDF: {ex.Message}");
            }
        }

        public async Task<string> GetInvoicePdfUrlAsync(Guid invoiceId)
        {
            var invoice = await context.StripeInvoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new Exception("Invoice not found");

            var features = appSettings.Subscriptions ?? new SubscriptionFeatures();
            if (!features.AllowInvoiceDownload)
                throw new Exception("Invoice download not enabled");

            if (string.IsNullOrEmpty(invoice.InvoicePdfUrl))
            {
                // Fetch latest invoice data from Stripe
                var service = new InvoiceService();
                var stripeInvoice = await service.GetAsync(invoice.StripeInvoiceId);
                invoice.InvoicePdfUrl = stripeInvoice.InvoicePdf;
                invoice.HostedInvoiceUrl = stripeInvoice.HostedInvoiceUrl;
                await context.SaveChangesAsync();
            }

            return invoice.InvoicePdfUrl;
        }

        public async Task<StripeInvoice> SyncInvoiceFromStripeAsync(string stripeInvoiceId)
        {
            var service = new InvoiceService();
            var options = new InvoiceGetOptions
            {
                Expand = new List<string> { "customer", "subscription", "lines.data", "charge", "payment_intent" }
            };

            var stripeInvoice = await service.GetAsync(stripeInvoiceId, options);

            var existingInvoice = await context.StripeInvoices
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(i => i.StripeInvoiceId == stripeInvoiceId);

            if (existingInvoice != null)
            {
                UpdateInvoiceFromStripe(existingInvoice, stripeInvoice);
                await context.SaveChangesAsync();
                return existingInvoice;
            }
            else
            {
                // Find wallet by customer ID
                var wallet = await context.Wallets
                    .FirstOrDefaultAsync(w => w.PaymentCustomerId == stripeInvoice.CustomerId);

                if (wallet == null)
                    throw new Exception($"No wallet found for customer {stripeInvoice.CustomerId}");

                return await SaveInvoiceToDatabase(stripeInvoice, wallet.Id);
            }
        }

        public async Task SyncAllInvoicesForWalletAsync(Guid walletId)
        {
            var wallet = await context.Wallets
                .FirstOrDefaultAsync(w => w.Id == walletId);

            if (wallet == null || string.IsNullOrEmpty(wallet.PaymentCustomerId))
                return;

            var service = new InvoiceService();
            var options = new InvoiceListOptions
            {
                Customer = wallet.PaymentCustomerId,
                Limit = 100,
                Expand = new List<string> { "data.lines.data", "data.subscription" }
            };

            var invoices = await service.ListAsync(options);

            foreach (var stripeInvoice in invoices.Data)
            {
                var existingInvoice = await context.StripeInvoices
                    .FirstOrDefaultAsync(i => i.StripeInvoiceId == stripeInvoice.Id);

                if (existingInvoice != null)
                {
                    UpdateInvoiceFromStripe(existingInvoice, stripeInvoice);
                }
                else
                {
                    await SaveInvoiceToDatabase(stripeInvoice, walletId);
                }
            }

            await context.SaveChangesAsync();
        }

        private async Task<StripeInvoice> SaveInvoiceToDatabase(
            Stripe.Invoice stripeInvoice, Guid walletId)
        {
            var invoice = new StripeInvoice
            {
                WalletId = walletId,
                StripeInvoiceId = stripeInvoice.Id,
                CustomerId = stripeInvoice.CustomerId,
                CreatedAt = new DateTimeOffset(stripeInvoice.Created),
                LastSyncedAt = DateTimeOffset.UtcNow
            };

            // Link to subscription if exists
            // SubscriptionId is not a property in this SDK version - it would be expanded as Subscription object
            // For now, skip subscription linking unless Subscription is expanded in the request

            UpdateInvoiceFromStripe(invoice, stripeInvoice);

            context.StripeInvoices.Add(invoice);
            await context.SaveChangesAsync();

            return invoice;
        }

        private void UpdateInvoiceFromStripe(
            StripeInvoice invoice,
            Stripe.Invoice stripeInvoice)
        {
            invoice.InvoiceNumber = stripeInvoice.Number;
            invoice.Status = MapStripeInvoiceStatus(stripeInvoice.Status);

            invoice.AmountDue = stripeInvoice.AmountDue / 100m;
            invoice.AmountPaid = stripeInvoice.AmountPaid / 100m;
            invoice.AmountRemaining = stripeInvoice.AmountRemaining / 100m;
            invoice.Subtotal = stripeInvoice.Subtotal / 100m;
            invoice.Tax = null; // Tax details would need to be calculated from line items if needed
            invoice.Total = stripeInvoice.Total / 100m;
            invoice.Currency = stripeInvoice.Currency;

            invoice.Description = stripeInvoice.Description;

            // Discount tracking would need custom implementation based on applied coupons

            if (stripeInvoice.DueDate.HasValue)
                invoice.DueDate = new DateTimeOffset(stripeInvoice.DueDate.Value);

            // Period dates are not direct properties on Stripe.Invoice
            // They come from line items if needed

            if (stripeInvoice.StatusTransitions?.PaidAt.HasValue == true)
                invoice.PaidAt = new DateTimeOffset(stripeInvoice.StatusTransitions.PaidAt.Value);

            invoice.AttemptCount = (int)stripeInvoice.AttemptCount;

            if (stripeInvoice.NextPaymentAttempt.HasValue)
                invoice.NextPaymentAttempt = new DateTimeOffset(stripeInvoice.NextPaymentAttempt.Value);

            if (stripeInvoice.StatusTransitions?.FinalizedAt.HasValue == true)
                invoice.FinalizedAt = new DateTimeOffset(stripeInvoice.StatusTransitions.FinalizedAt.Value);

            invoice.PaymentMethodId = stripeInvoice.DefaultPaymentMethodId;
            invoice.ChargeId = null; // Charge property not available directly in this SDK version
            invoice.PaymentIntentId = null; // PaymentIntent property not available directly in this SDK version
            invoice.HostedInvoiceUrl = stripeInvoice.HostedInvoiceUrl;
            invoice.InvoicePdfUrl = stripeInvoice.InvoicePdf;
            invoice.AutoAdvance = stripeInvoice.AutoAdvance;
            invoice.CollectionMethod = stripeInvoice.CollectionMethod;
            invoice.BillingReason = stripeInvoice.BillingReason;

            if (stripeInvoice.Metadata != null)
                invoice.Metadata = JsonSerializer.Serialize(stripeInvoice.Metadata);

            // Update line items
            if (stripeInvoice.Lines?.Data?.Any() == true)
            {
                foreach (var stripeLineItem in stripeInvoice.Lines.Data)
                {
                    var existingLineItem = invoice.LineItems?.FirstOrDefault(li => li.StripeLineItemId == stripeLineItem.Id);

                    if (existingLineItem != null)
                    {
                        existingLineItem.Description = stripeLineItem.Description;
                        existingLineItem.Quantity = (int)(stripeLineItem.Quantity ?? 1);
                        existingLineItem.Amount = stripeLineItem.Amount / 100m;
                    }
                    else
                    {
                        var newLineItem = new StripeInvoiceLineItem
                        {
                            StripeInvoiceId = invoice.Id,
                            StripeLineItemId = stripeLineItem.Id,
                            Type = "line_item", // Type property not available in this SDK version
                            Description = stripeLineItem.Description,
                            Quantity = (int)(stripeLineItem.Quantity ?? 1),
                            Amount = stripeLineItem.Amount / 100m,
                            Currency = stripeLineItem.Currency,
                            Proration = false, // Proration details not available in this SDK version
                            PriceId = null, // Plan property not available on InvoiceLineItem in this SDK version
                            SubscriptionId = null, // Subscription as string not available on InvoiceLineItem
                            SubscriptionItemId = null // Not available in this SDK version
                        };

                        if (stripeLineItem.Period != null)
                        {
                            newLineItem.PeriodStart = new DateTimeOffset(stripeLineItem.Period.Start);
                            newLineItem.PeriodEnd = new DateTimeOffset(stripeLineItem.Period.End);
                        }

                        if (stripeLineItem.Metadata != null)
                            newLineItem.Metadata = JsonSerializer.Serialize(stripeLineItem.Metadata);

                        if (invoice.LineItems == null)
                            invoice.LineItems = new List<StripeInvoiceLineItem>();

                        invoice.LineItems.Add(newLineItem);
                    }
                }
            }

            invoice.LastSyncedAt = DateTimeOffset.UtcNow;
        }

        private StripeInvoiceStatus MapStripeInvoiceStatus(string stripeStatus)
        {
            return stripeStatus switch
            {
                "draft" => StripeInvoiceStatus.Draft,
                "open" => StripeInvoiceStatus.Open,
                "paid" => StripeInvoiceStatus.Paid,
                "uncollectible" => StripeInvoiceStatus.Uncollectible,
                "void" => StripeInvoiceStatus.Void,
                "deleted" => StripeInvoiceStatus.Deleted,
                _ => StripeInvoiceStatus.Open
            };
        }
    }

    #region Result Classes

    public class InvoiceResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public StripeInvoice Invoice { get; set; }
    }

    public class InvoiceFilter
    {
        public StripeInvoiceStatus? Status { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public Guid? SubscriptionId { get; set; }
        public int Limit { get; set; } = 100;
    }

    #endregion
}
