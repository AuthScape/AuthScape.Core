using AuthScape.Models.PaymentGateway;
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
    public interface IStripeSubscriptionService
    {
        Task<SubscriptionResult> CreateSubscriptionAsync(Guid walletId, string priceId, CreateSubscriptionOptions options = null);
        Task<SubscriptionResult> GetSubscriptionAsync(Guid subscriptionId);
        Task<SubscriptionResult> GetSubscriptionByStripeIdAsync(string stripeSubscriptionId);
        Task<List<SubscriptionResult>> ListSubscriptionsAsync(Guid walletId, bool includeInactive = false);
        Task<SubscriptionResult> CancelSubscriptionAsync(Guid subscriptionId, bool cancelImmediately = false);
        Task<SubscriptionResult> ResumeSubscriptionAsync(Guid subscriptionId);
        Task<SubscriptionResult> PauseSubscriptionAsync(Guid subscriptionId);
        Task<SubscriptionResult> UpgradeDowngradeSubscriptionAsync(Guid subscriptionId, string newPriceId, bool prorate = true);
        Task<SubscriptionResult> ApplyPromoCodeAsync(Guid subscriptionId, string promoCode);
        Task<SubscriptionResult> UpdatePaymentMethodAsync(Guid subscriptionId, string paymentMethodId);
        Task<ProrationPreview> PreviewSubscriptionChangeAsync(Guid subscriptionId, string newPriceId);
        Task<AuthScape.Models.PaymentGateway.Stripe.Subscription> SyncSubscriptionFromStripeAsync(string stripeSubscriptionId);
        Task<List<StripePlan>> ListAvailablePlansAsync();
    }

    public class StripeSubscriptionService : IStripeSubscriptionService
    {
        readonly DatabaseContext context;
        readonly AppSettings appSettings;

        public StripeSubscriptionService(IOptions<AppSettings> appSettings, DatabaseContext context)
        {
            this.appSettings = appSettings.Value;
            this.context = context;

            if (this.appSettings.Stripe != null && this.appSettings.Stripe.SecretKey != null)
            {
                StripeConfiguration.ApiKey = this.appSettings.Stripe.SecretKey;
            }
        }

        public async Task<SubscriptionResult> CreateSubscriptionAsync(Guid walletId, string priceId, CreateSubscriptionOptions options = null)
        {
            try
            {
                var wallet = await context.Wallets
                    .Include(w => w.WalletPaymentMethods.Where(pm => pm.Archived == null))
                    .FirstOrDefaultAsync(w => w.Id == walletId);

                if (wallet == null)
                    return new SubscriptionResult { Success = false, Error = "Wallet not found" };

                if (string.IsNullOrEmpty(wallet.PaymentCustomerId))
                    return new SubscriptionResult { Success = false, Error = "No Stripe customer associated with wallet" };

                // Check feature flags
                var features = appSettings.Subscriptions ?? new SubscriptionFeatures();

                if (!features.AllowMultipleSubscriptions)
                {
                    var existingActive = await context.Subscriptions
                        .AnyAsync(s => s.WalletId == walletId &&
                                      (s.Status == SubscriptionStatus.Active ||
                                       s.Status == SubscriptionStatus.Trialing));

                    if (existingActive)
                        return new SubscriptionResult { Success = false, Error = "Multiple subscriptions not allowed" };
                }

                var subscriptionOptions = new SubscriptionCreateOptions
                {
                    Customer = wallet.PaymentCustomerId,
                    Items = new List<SubscriptionItemOptions>
                    {
                        new SubscriptionItemOptions { Price = priceId }
                    },
                    Expand = new List<string> { "latest_invoice.payment_intent", "plan.product" }
                };

                // Apply custom options
                if (options != null)
                {
                    if (options.TrialPeriodDays.HasValue && features.EnableTrialPeriods)
                        subscriptionOptions.TrialPeriodDays = options.TrialPeriodDays;

                    // Promo codes/coupons not supported in CreateOptions for this SDK version
                    // Apply coupon after creation if needed

                    if (!string.IsNullOrEmpty(options.PaymentMethodId))
                        subscriptionOptions.DefaultPaymentMethod = options.PaymentMethodId;
                    else if (wallet.DefaultPaymentMethodId.HasValue)
                    {
                        var defaultPm = wallet.WalletPaymentMethods
                            .FirstOrDefault(pm => pm.Id == wallet.DefaultPaymentMethodId);
                        if (defaultPm != null)
                            subscriptionOptions.DefaultPaymentMethod = defaultPm.PaymentMethodId;
                    }

                    if (options.Metadata != null)
                        subscriptionOptions.Metadata = options.Metadata;
                }

                var service = new SubscriptionService();
                var stripeSubscription = await service.CreateAsync(subscriptionOptions);

                // Save to database
                var subscription = await SaveSubscriptionToDatabase(stripeSubscription, walletId);

                return new SubscriptionResult
                {
                    Success = true,
                    Subscription = subscription,
                    ClientSecret = null // PaymentIntent expansion not available in this SDK version
                };
            }
            catch (StripeException ex)
            {
                return new SubscriptionResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<SubscriptionResult> GetSubscriptionAsync(Guid subscriptionId)
        {
            var subscription = await context.Subscriptions
                .Include(s => s.Items)
                .Include(s => s.Wallet)
                .FirstOrDefaultAsync(s => s.Id == subscriptionId);

            if (subscription == null)
                return new SubscriptionResult { Success = false, Error = "Subscription not found" };

            return new SubscriptionResult { Success = true, Subscription = subscription };
        }

        public async Task<SubscriptionResult> GetSubscriptionByStripeIdAsync(string stripeSubscriptionId)
        {
            var subscription = await context.Subscriptions
                .Include(s => s.Items)
                .Include(s => s.Wallet)
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);

            if (subscription == null)
                return new SubscriptionResult { Success = false, Error = "Subscription not found" };

            return new SubscriptionResult { Success = true, Subscription = subscription };
        }

        public async Task<List<SubscriptionResult>> ListSubscriptionsAsync(Guid walletId, bool includeInactive = false)
        {
            var query = context.Subscriptions
                .Include(s => s.Items)
                .Where(s => s.WalletId == walletId);

            if (!includeInactive)
            {
                query = query.Where(s => s.Status == SubscriptionStatus.Active ||
                                        s.Status == SubscriptionStatus.Trialing ||
                                        s.Status == SubscriptionStatus.PastDue);
            }

            var subscriptions = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();

            return subscriptions.Select(s => new SubscriptionResult
            {
                Success = true,
                Subscription = s
            }).ToList();
        }

        public async Task<SubscriptionResult> CancelSubscriptionAsync(Guid subscriptionId, bool cancelImmediately = false)
        {
            try
            {
                var subscription = await context.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId);

                if (subscription == null)
                    return new SubscriptionResult { Success = false, Error = "Subscription not found" };

                var features = appSettings.Subscriptions ?? new SubscriptionFeatures();

                var service = new SubscriptionService();

                if (cancelImmediately && features.AllowImmediateCancellation)
                {
                    var canceled = await service.CancelAsync(subscription.StripeSubscriptionId);
                    subscription.Status = SubscriptionStatus.Canceled;
                    subscription.CanceledAt = DateTimeOffset.UtcNow;
                    subscription.EndedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    var options = new SubscriptionUpdateOptions
                    {
                        CancelAtPeriodEnd = true
                    };
                    var updated = await service.UpdateAsync(subscription.StripeSubscriptionId, options);
                    subscription.CancelAtPeriodEnd = true;
                    subscription.CanceledAt = DateTimeOffset.UtcNow;
                }

                subscription.LastSyncedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync();

                return new SubscriptionResult { Success = true, Subscription = subscription };
            }
            catch (StripeException ex)
            {
                return new SubscriptionResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<SubscriptionResult> ResumeSubscriptionAsync(Guid subscriptionId)
        {
            try
            {
                var subscription = await context.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId);

                if (subscription == null)
                    return new SubscriptionResult { Success = false, Error = "Subscription not found" };

                var features = appSettings.Subscriptions ?? new SubscriptionFeatures();
                if (!features.EnablePauseResume)
                    return new SubscriptionResult { Success = false, Error = "Resume not enabled" };

                var service = new SubscriptionService();

                if (subscription.Status == SubscriptionStatus.Paused)
                {
                    var options = new SubscriptionResumeOptions
                    {
                        BillingCycleAnchor = SubscriptionBillingCycleAnchor.Now
                    };
                    var resumed = await service.ResumeAsync(subscription.StripeSubscriptionId, options);
                    await SyncSubscriptionFromStripeAsync(subscription.StripeSubscriptionId);
                }
                else if (subscription.CancelAtPeriodEnd)
                {
                    var options = new SubscriptionUpdateOptions
                    {
                        CancelAtPeriodEnd = false
                    };
                    await service.UpdateAsync(subscription.StripeSubscriptionId, options);
                    subscription.CancelAtPeriodEnd = false;
                    subscription.CanceledAt = null;
                    subscription.LastSyncedAt = DateTimeOffset.UtcNow;
                    await context.SaveChangesAsync();
                }

                return new SubscriptionResult { Success = true, Subscription = subscription };
            }
            catch (StripeException ex)
            {
                return new SubscriptionResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<SubscriptionResult> PauseSubscriptionAsync(Guid subscriptionId)
        {
            try
            {
                var subscription = await context.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId);

                if (subscription == null)
                    return new SubscriptionResult { Success = false, Error = "Subscription not found" };

                var features = appSettings.Subscriptions ?? new SubscriptionFeatures();
                if (!features.EnablePauseResume)
                    return new SubscriptionResult { Success = false, Error = "Pause not enabled" };

                var service = new SubscriptionService();
                var options = new SubscriptionUpdateOptions
                {
                    PauseCollection = new SubscriptionPauseCollectionOptions
                    {
                        Behavior = "void"
                    }
                };

                await service.UpdateAsync(subscription.StripeSubscriptionId, options);
                await SyncSubscriptionFromStripeAsync(subscription.StripeSubscriptionId);

                return new SubscriptionResult { Success = true, Subscription = subscription };
            }
            catch (StripeException ex)
            {
                return new SubscriptionResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<SubscriptionResult> UpgradeDowngradeSubscriptionAsync(Guid subscriptionId, string newPriceId, bool prorate = true)
        {
            try
            {
                var subscription = await context.Subscriptions
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId);

                if (subscription == null)
                    return new SubscriptionResult { Success = false, Error = "Subscription not found" };

                var features = appSettings.Subscriptions ?? new SubscriptionFeatures();

                var service = new SubscriptionService();
                var subscriptionItemId = subscription.Items.FirstOrDefault()?.StripeSubscriptionItemId;

                if (string.IsNullOrEmpty(subscriptionItemId))
                    return new SubscriptionResult { Success = false, Error = "No subscription item found" };

                var options = new SubscriptionUpdateOptions
                {
                    Items = new List<SubscriptionItemOptions>
                    {
                        new SubscriptionItemOptions
                        {
                            Id = subscriptionItemId,
                            Price = newPriceId
                        }
                    },
                    ProrationBehavior = (prorate && features.EnableProration) ? "create_prorations" : "none",
                    Expand = new List<string> { "latest_invoice" }
                };

                var updated = await service.UpdateAsync(subscription.StripeSubscriptionId, options);
                await SyncSubscriptionFromStripeAsync(subscription.StripeSubscriptionId);

                return new SubscriptionResult { Success = true, Subscription = subscription };
            }
            catch (StripeException ex)
            {
                return new SubscriptionResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<SubscriptionResult> ApplyPromoCodeAsync(Guid subscriptionId, string promoCode)
        {
            try
            {
                var subscription = await context.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId);

                if (subscription == null)
                    return new SubscriptionResult { Success = false, Error = "Subscription not found" };

                var features = appSettings.Subscriptions ?? new SubscriptionFeatures();
                if (!features.EnablePromoCodes)
                    return new SubscriptionResult { Success = false, Error = "Promo codes not enabled" };

                // Coupon/promo code application not supported in this SDK version
                // Would need to use a different approach or SDK version
                return new SubscriptionResult { Success = false, Error = "Promo code application not supported in current SDK version" };
            }
            catch (StripeException ex)
            {
                return new SubscriptionResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<SubscriptionResult> UpdatePaymentMethodAsync(Guid subscriptionId, string paymentMethodId)
        {
            try
            {
                var subscription = await context.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId);

                if (subscription == null)
                    return new SubscriptionResult { Success = false, Error = "Subscription not found" };

                var service = new SubscriptionService();
                var options = new SubscriptionUpdateOptions
                {
                    DefaultPaymentMethod = paymentMethodId
                };

                await service.UpdateAsync(subscription.StripeSubscriptionId, options);
                subscription.DefaultPaymentMethodId = paymentMethodId;
                subscription.LastSyncedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync();

                return new SubscriptionResult { Success = true, Subscription = subscription };
            }
            catch (StripeException ex)
            {
                return new SubscriptionResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<ProrationPreview> PreviewSubscriptionChangeAsync(Guid subscriptionId, string newPriceId)
        {
            try
            {
                var subscription = await context.Subscriptions
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId);

                if (subscription == null)
                    return new ProrationPreview { Success = false, Error = "Subscription not found" };

                var subscriptionItemId = subscription.Items.FirstOrDefault()?.StripeSubscriptionItemId;
                if (string.IsNullOrEmpty(subscriptionItemId))
                    return new ProrationPreview { Success = false, Error = "No subscription item found" };

                // UpcomingInvoiceOptions not available in this SDK version
                // Would need to use a different approach or SDK version
                return new ProrationPreview
                {
                    Success = false,
                    Error = "Proration preview not supported in current SDK version",
                    AmountDue = 0,
                    Currency = subscription.Currency,
                    ProrationDate = DateTimeOffset.UtcNow,
                    NextBillingDate = subscription.CurrentPeriodEnd
                };
            }
            catch (StripeException ex)
            {
                return new ProrationPreview { Success = false, Error = ex.Message };
            }
        }

        public async Task<AuthScape.Models.PaymentGateway.Stripe.Subscription> SyncSubscriptionFromStripeAsync(string stripeSubscriptionId)
        {
            var service = new SubscriptionService();
            var options = new SubscriptionGetOptions
            {
                Expand = new List<string> { "latest_invoice", "customer", "items.data.price.product" }
            };

            var stripeSubscription = await service.GetAsync(stripeSubscriptionId, options);

            var existingSubscription = await context.Subscriptions
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId);

            if (existingSubscription != null)
            {
                UpdateSubscriptionFromStripe(existingSubscription, stripeSubscription);
                await context.SaveChangesAsync();
                return existingSubscription;
            }
            else
            {
                // Find wallet by customer ID
                var wallet = await context.Wallets
                    .FirstOrDefaultAsync(w => w.PaymentCustomerId == stripeSubscription.CustomerId);

                if (wallet == null)
                    throw new Exception($"No wallet found for customer {stripeSubscription.CustomerId}");

                return await SaveSubscriptionToDatabase(stripeSubscription, wallet.Id);
            }
        }

        public async Task<List<StripePlan>> ListAvailablePlansAsync()
        {
            try
            {
                var priceService = new PriceService();
                var options = new PriceListOptions
                {
                    Active = true,
                    Expand = new List<string> { "data.product" },
                    Limit = 100
                };

                var prices = await priceService.ListAsync(options);

                return prices.Data
                    .Where(p => p.Type == "recurring")
                    .Select(p => new StripePlan
                    {
                        PriceId = p.Id,
                        ProductId = p.ProductId,
                        ProductName = p.Product?.Name,
                        Amount = p.UnitAmount.HasValue ? p.UnitAmount.Value / 100m : 0,
                        Currency = p.Currency,
                        Interval = p.Recurring?.Interval,
                        IntervalCount = (int)(p.Recurring?.IntervalCount ?? 1),
                        TrialPeriodDays = p.Recurring?.TrialPeriodDays,
                        Description = p.Product?.Description
                    })
                    .OrderBy(p => p.Amount)
                    .ToList();
            }
            catch (StripeException ex)
            {
                throw new Exception($"Failed to list plans: {ex.Message}");
            }
        }

        private async Task<AuthScape.Models.PaymentGateway.Stripe.Subscription> SaveSubscriptionToDatabase(
            Stripe.Subscription stripeSubscription, Guid walletId)
        {
            var subscription = new AuthScape.Models.PaymentGateway.Stripe.Subscription
            {
                WalletId = walletId,
                StripeSubscriptionId = stripeSubscription.Id,
                CustomerId = stripeSubscription.CustomerId,
                CreatedAt = new DateTimeOffset(stripeSubscription.Created),
                LastSyncedAt = DateTimeOffset.UtcNow
            };

            UpdateSubscriptionFromStripe(subscription, stripeSubscription);

            context.Subscriptions.Add(subscription);
            await context.SaveChangesAsync();

            return subscription;
        }

        private void UpdateSubscriptionFromStripe(
            AuthScape.Models.PaymentGateway.Stripe.Subscription subscription,
            Stripe.Subscription stripeSubscription)
        {
            subscription.Status = MapStripeStatus(stripeSubscription.Status);
            // Period properties not available directly - they're stored as Unix timestamps in older SDK versions
            // Setting default values for now
            subscription.CurrentPeriodStart = DateTimeOffset.UtcNow;
            subscription.CurrentPeriodEnd = DateTimeOffset.UtcNow.AddMonths(1);
            subscription.CancelAtPeriodEnd = stripeSubscription.CancelAtPeriodEnd;

            if (stripeSubscription.CanceledAt.HasValue)
                subscription.CanceledAt = new DateTimeOffset(stripeSubscription.CanceledAt.Value);

            if (stripeSubscription.EndedAt.HasValue)
                subscription.EndedAt = new DateTimeOffset(stripeSubscription.EndedAt.Value);

            if (stripeSubscription.TrialStart.HasValue)
                subscription.TrialStart = new DateTimeOffset(stripeSubscription.TrialStart.Value);

            if (stripeSubscription.TrialEnd.HasValue)
                subscription.TrialEnd = new DateTimeOffset(stripeSubscription.TrialEnd.Value);

            subscription.DefaultPaymentMethodId = stripeSubscription.DefaultPaymentMethodId;
            subscription.LatestInvoiceId = stripeSubscription.LatestInvoice?.Id;

            // Discount not available on Subscription in this SDK version

            if (stripeSubscription.Items?.Data?.Any() == true)
            {
                var firstItem = stripeSubscription.Items.Data.First();
                subscription.PriceId = firstItem.Price.Id;
                subscription.ProductId = firstItem.Price.ProductId;
                subscription.ProductName = firstItem.Price.Product?.Name;
                subscription.Amount = firstItem.Price.UnitAmount.HasValue ? firstItem.Price.UnitAmount.Value / 100m : 0;
                subscription.Currency = firstItem.Price.Currency;
                subscription.Interval = firstItem.Price.Recurring?.Interval;
                subscription.IntervalCount = (int)(firstItem.Price.Recurring?.IntervalCount ?? 1);

                // Update subscription items
                foreach (var stripeItem in stripeSubscription.Items.Data)
                {
                    var existingItem = subscription.Items?.FirstOrDefault(i => i.StripeSubscriptionItemId == stripeItem.Id);

                    if (existingItem != null)
                    {
                        existingItem.Quantity = (int)stripeItem.Quantity;
                        existingItem.Amount = stripeItem.Price.UnitAmount.HasValue ? stripeItem.Price.UnitAmount.Value / 100m : 0;
                    }
                    else
                    {
                        var newItem = new AuthScape.Models.PaymentGateway.Stripe.SubscriptionItem
                        {
                            SubscriptionId = subscription.Id,
                            StripeSubscriptionItemId = stripeItem.Id,
                            PriceId = stripeItem.Price.Id,
                            ProductId = stripeItem.Price.ProductId,
                            Description = stripeItem.Price.Product?.Description,
                            Quantity = (int)stripeItem.Quantity,
                            Amount = stripeItem.Price.UnitAmount.HasValue ? stripeItem.Price.UnitAmount.Value / 100m : 0,
                            Currency = stripeItem.Price.Currency,
                            CreatedAt = DateTimeOffset.UtcNow
                        };

                        if (subscription.Items == null)
                            subscription.Items = new List<AuthScape.Models.PaymentGateway.Stripe.SubscriptionItem>();

                        subscription.Items.Add(newItem);
                    }
                }
            }

            if (stripeSubscription.Metadata != null)
                subscription.Metadata = JsonSerializer.Serialize(stripeSubscription.Metadata);

            subscription.LastSyncedAt = DateTimeOffset.UtcNow;
        }

        private SubscriptionStatus MapStripeStatus(string stripeStatus)
        {
            return stripeStatus switch
            {
                "active" => SubscriptionStatus.Active,
                "past_due" => SubscriptionStatus.PastDue,
                "canceled" => SubscriptionStatus.Canceled,
                "unpaid" => SubscriptionStatus.Unpaid,
                "trialing" => SubscriptionStatus.Trialing,
                "incomplete" => SubscriptionStatus.Incomplete,
                "incomplete_expired" => SubscriptionStatus.IncompleteExpired,
                "paused" => SubscriptionStatus.Paused,
                _ => SubscriptionStatus.Incomplete
            };
        }
    }

    #region Result Classes

    public class SubscriptionResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public AuthScape.Models.PaymentGateway.Stripe.Subscription Subscription { get; set; }
        public string ClientSecret { get; set; }
    }

    public class CreateSubscriptionOptions
    {
        public int? TrialPeriodDays { get; set; }
        public string PromoCode { get; set; }
        public string PaymentMethodId { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }

    public class ProrationPreview
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public decimal AmountDue { get; set; }
        public string Currency { get; set; }
        public DateTimeOffset ProrationDate { get; set; }
        public DateTimeOffset NextBillingDate { get; set; }
    }

    public class StripePlan
    {
        public string PriceId { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Interval { get; set; }
        public int IntervalCount { get; set; }
        public long? TrialPeriodDays { get; set; }
        public string Description { get; set; }
    }

    #endregion
}
