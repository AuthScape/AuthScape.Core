using AuthScape.Models.PaymentGateway.Stripe;
using AuthScape.Services;
using AuthScape.StripePayment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
using Services.Context;
using Services.Database;

namespace AuthScape.StripePayment.Controller.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme + ",Identity.Application")]
    public class InvoiceController : ControllerBase
    {
        readonly DatabaseContext context;
        readonly AppSettings appSettings;
        readonly IUserManagementService userManagementService;
        readonly IStripeInvoiceService invoiceService;

        public InvoiceController(
            IOptions<AppSettings> appSettings,
            DatabaseContext context,
            IUserManagementService userManagementService,
            IStripeInvoiceService invoiceService)
        {
            this.appSettings = appSettings.Value;
            this.context = context;
            this.userManagementService = userManagementService;
            this.invoiceService = invoiceService;
        }

        /// <summary>
        /// Get all invoices for the current user/company
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyInvoices(
            [FromQuery] string walletType = "user",
            [FromQuery] StripeInvoiceStatus? status = null,
            [FromQuery] DateTimeOffset? startDate = null,
            [FromQuery] DateTimeOffset? endDate = null,
            [FromQuery] int limit = 100)
        {
            try
            {
                // Get user ID from claims (works with both cookie and bearer token auth)
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                // Get the appropriate wallet
                Guid walletId;
                if (walletType == "company")
                {
                    // Try to get company ID from claims
                    var companyIdValue = User.FindFirst("companyId")?.Value;
                    if (long.TryParse(companyIdValue, out var companyId))
                    {
                        var wallet = await context.Wallets
                            .FirstOrDefaultAsync(w => w.CompanyId == companyId);
                        if (wallet == null)
                            return Ok(new { success = true, invoices = new List<object>() });
                        walletId = wallet.Id;
                    }
                    else
                    {
                        return BadRequest(new { success = false, error = "Company wallet requested but no company ID found" });
                    }
                }
                else
                {
                    var wallet = await context.Wallets
                        .FirstOrDefaultAsync(w => w.UserId == userId);
                    if (wallet == null)
                        return Ok(new { success = true, invoices = new List<object>() });
                    walletId = wallet.Id;
                }

                var filter = new InvoiceFilter
                {
                    Status = status,
                    StartDate = startDate,
                    EndDate = endDate,
                    Limit = limit
                };

                var results = await invoiceService.ListInvoicesAsync(walletId, filter);

                var invoices = results.Select(r => new
                {
                    id = r.Invoice.Id,
                    stripeInvoiceId = r.Invoice.StripeInvoiceId,
                    invoiceNumber = r.Invoice.InvoiceNumber,
                    status = r.Invoice.Status.ToString(),
                    amountDue = r.Invoice.AmountDue,
                    amountPaid = r.Invoice.AmountPaid,
                    amountRemaining = r.Invoice.AmountRemaining,
                    subtotal = r.Invoice.Subtotal,
                    tax = r.Invoice.Tax,
                    total = r.Invoice.Total,
                    currency = r.Invoice.Currency,
                    description = r.Invoice.Description,
                    createdAt = r.Invoice.CreatedAt,
                    dueDate = r.Invoice.DueDate,
                    paidAt = r.Invoice.PaidAt,
                    periodStart = r.Invoice.PeriodStart,
                    periodEnd = r.Invoice.PeriodEnd,
                    hostedInvoiceUrl = r.Invoice.HostedInvoiceUrl,
                    invoicePdfUrl = r.Invoice.InvoicePdfUrl,
                    subscriptionId = r.Invoice.SubscriptionId,
                    attemptCount = r.Invoice.AttemptCount,
                    nextPaymentAttempt = r.Invoice.NextPaymentAttempt,
                    lineItemCount = r.Invoice.LineItems?.Count ?? 0
                }).ToList();

                return Ok(new { success = true, invoices });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific invoice by ID
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetInvoice([FromQuery] Guid invoiceId)
        {
            try
            {
                var result = await invoiceService.GetInvoiceAsync(invoiceId);

                if (!result.Success)
                    return NotFound(new { success = false, error = result.Error });

                var invoice = result.Invoice;
                return Ok(new
                {
                    success = true,
                    invoice = new
                    {
                        id = invoice.Id,
                        stripeInvoiceId = invoice.StripeInvoiceId,
                        invoiceNumber = invoice.InvoiceNumber,
                        status = invoice.Status.ToString(),
                        amountDue = invoice.AmountDue,
                        amountPaid = invoice.AmountPaid,
                        amountRemaining = invoice.AmountRemaining,
                        subtotal = invoice.Subtotal,
                        tax = invoice.Tax,
                        total = invoice.Total,
                        currency = invoice.Currency,
                        description = invoice.Description,
                        createdAt = invoice.CreatedAt,
                        dueDate = invoice.DueDate,
                        paidAt = invoice.PaidAt,
                        periodStart = invoice.PeriodStart,
                        periodEnd = invoice.PeriodEnd,
                        hostedInvoiceUrl = invoice.HostedInvoiceUrl,
                        invoicePdfUrl = invoice.InvoicePdfUrl,
                        attemptCount = invoice.AttemptCount,
                        nextPaymentAttempt = invoice.NextPaymentAttempt,
                        billingReason = invoice.BillingReason,
                        lineItems = invoice.LineItems?.Select(li => new
                        {
                            id = li.Id,
                            description = li.Description,
                            quantity = li.Quantity,
                            amount = li.Amount,
                            currency = li.Currency,
                            proration = li.Proration,
                            periodStart = li.PeriodStart,
                            periodEnd = li.PeriodEnd
                        }).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Retry payment on a failed invoice
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RetryPayment([FromBody] RetryPaymentRequest req)
        {
            try
            {
                var result = await invoiceService.RetryPaymentAsync(req.InvoiceId);

                if (!result.Success)
                    return BadRequest(new { success = false, error = result.Error });

                return Ok(new
                {
                    success = true,
                    invoice = new
                    {
                        id = result.Invoice.Id,
                        status = result.Invoice.Status.ToString(),
                        amountPaid = result.Invoice.AmountPaid,
                        amountRemaining = result.Invoice.AmountRemaining,
                        paidAt = result.Invoice.PaidAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Download invoice as PDF
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DownloadInvoicePdf([FromQuery] Guid invoiceId)
        {
            try
            {
                var pdfBytes = await invoiceService.DownloadInvoicePdfAsync(invoiceId);

                var result = await invoiceService.GetInvoiceAsync(invoiceId);
                var filename = $"invoice-{result.Invoice.InvoiceNumber ?? invoiceId.ToString()}.pdf";

                return File(pdfBytes, "application/pdf", filename);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get PDF URL for an invoice
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetInvoicePdfUrl([FromQuery] Guid invoiceId)
        {
            try
            {
                var url = await invoiceService.GetInvoicePdfUrlAsync(invoiceId);

                return Ok(new
                {
                    success = true,
                    pdfUrl = url
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Sync invoice data from Stripe
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SyncInvoice([FromBody] SyncInvoiceRequest req)
        {
            try
            {
                var invoice = await invoiceService.SyncInvoiceFromStripeAsync(req.StripeInvoiceId);

                return Ok(new
                {
                    success = true,
                    invoice = new
                    {
                        id = invoice.Id,
                        stripeInvoiceId = invoice.StripeInvoiceId,
                        status = invoice.Status.ToString(),
                        total = invoice.Total,
                        lastSyncedAt = invoice.LastSyncedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Sync all invoices for the current user's wallet
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SyncAllInvoices([FromQuery] string walletType = "user")
        {
            try
            {
                // Get user ID from claims (works with both cookie and bearer token auth)
                var idValue =
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                    User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ??
                    User.FindFirst("sub")?.Value ??
                    User.FindFirst("oid")?.Value;

                if (string.IsNullOrWhiteSpace(idValue) || !long.TryParse(idValue, out var userId))
                {
                    return Unauthorized(new { success = false, error = "User ID not found in claims" });
                }

                Guid walletId;
                if (walletType == "company")
                {
                    // Try to get company ID from claims
                    var companyIdValue = User.FindFirst("companyId")?.Value;
                    if (long.TryParse(companyIdValue, out var companyId))
                    {
                        var wallet = await context.Wallets
                            .FirstOrDefaultAsync(w => w.CompanyId == companyId);
                        if (wallet == null)
                            return BadRequest(new { success = false, error = "No company wallet found" });
                        walletId = wallet.Id;
                    }
                    else
                    {
                        return BadRequest(new { success = false, error = "Company wallet requested but no company ID found" });
                    }
                }
                else
                {
                    var wallet = await context.Wallets
                        .FirstOrDefaultAsync(w => w.UserId == userId);
                    if (wallet == null)
                        return BadRequest(new { success = false, error = "No user wallet found" });
                    walletId = wallet.Id;
                }

                await invoiceService.SyncAllInvoicesForWalletAsync(walletId);

                return Ok(new { success = true, message = "All invoices synced successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }

    #region Request Models

    public class RetryPaymentRequest
    {
        public Guid InvoiceId { get; set; }
    }

    public class SyncInvoiceRequest
    {
        public string StripeInvoiceId { get; set; }
    }

    #endregion
}
