using AuthScape.Models.PaymentGateway;
using AuthScape.Models.PaymentGateway.Stripe;
using AuthScape.StripePayment.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;
using Stripe;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AuthScape.IDP.Controllers
{
    [ApiController]
    [Route("stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly DatabaseContext _db;
        private readonly ILogger<StripeWebhookController> _logger;
        private readonly IStripeSubscriptionService _subscriptionService;
        private readonly IStripeInvoiceService _invoiceService;
        private readonly string _webhookSecret;
        private readonly AppSettings _appSettings;

        public StripeWebhookController(
            DatabaseContext db,
            IOptions<AppSettings> cfg,
            ILogger<StripeWebhookController> logger,
            IStripeSubscriptionService subscriptionService,
            IStripeInvoiceService invoiceService)
        {
            _db = db;
            _logger = logger;
            _subscriptionService = subscriptionService;
            _invoiceService = invoiceService;
            _appSettings = cfg.Value;
            _webhookSecret = cfg.Value.Stripe.SigningSecret;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            Event stripeEvent;

            try
            {
                stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], _webhookSecret);
                _logger.LogInformation($"Stripe webhook received: {stripeEvent.Type}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid webhook signature");
                return BadRequest();
            }

            try
            {
                switch (stripeEvent.Type)
                {
                    // ============ PAYMENT INTENT EVENTS ============
                    case "payment_intent.succeeded":
                        await HandlePaymentIntentSucceeded(stripeEvent);
                        break;

                    case "payment_intent.processing":
                        _logger.LogInformation("Payment processing...");
                        break;

                    case "payment_intent.payment_failed":
                        await HandlePaymentIntentFailed(stripeEvent);
                        break;

                    // ============ SETUP INTENT EVENTS ============
                    case "setup_intent.succeeded":
                        _logger.LogInformation("Setup intent succeeded");
                        break;

                    // ============ SUBSCRIPTION EVENTS ============
                    case "customer.subscription.created":
                        await HandleSubscriptionCreated(stripeEvent);
                        break;

                    case "customer.subscription.updated":
                        await HandleSubscriptionUpdated(stripeEvent);
                        break;

                    case "customer.subscription.deleted":
                        await HandleSubscriptionDeleted(stripeEvent);
                        break;

                    case "customer.subscription.trial_will_end":
                        await HandleSubscriptionTrialWillEnd(stripeEvent);
                        break;

                    case "customer.subscription.paused":
                        await HandleSubscriptionPaused(stripeEvent);
                        break;

                    case "customer.subscription.resumed":
                        await HandleSubscriptionResumed(stripeEvent);
                        break;

                    // ============ INVOICE EVENTS ============
                    case "invoice.created":
                        await HandleInvoiceCreated(stripeEvent);
                        break;

                    case "invoice.finalized":
                        await HandleInvoiceFinalized(stripeEvent);
                        break;

                    case "invoice.paid":
                        await HandleInvoicePaid(stripeEvent);
                        break;

                    case "invoice.payment_failed":
                        await HandleInvoicePaymentFailed(stripeEvent);
                        break;

                    case "invoice.payment_action_required":
                        await HandleInvoicePaymentActionRequired(stripeEvent);
                        break;

                    case "invoice.upcoming":
                        await HandleInvoiceUpcoming(stripeEvent);
                        break;

                    // ============ PAYMENT METHOD EVENTS ============
                    case "payment_method.attached":
                        _logger.LogInformation("Payment method attached");
                        break;

                    case "payment_method.detached":
                        await HandlePaymentMethodDetached(stripeEvent);
                        break;

                    default:
                        _logger.LogInformation($"Unhandled webhook event type: {stripeEvent.Type}");
                        break;
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing webhook event {stripeEvent.Type}");
                // Return 200 to prevent Stripe from retrying
                return Ok();
            }
        }

        #region Payment Intent Handlers

        private async Task HandlePaymentIntentSucceeded(Event stripeEvent)
        {
            var pi = stripeEvent.Data.Object as PaymentIntent;
            var walletId = pi?.Metadata?["wallet_id"];
            var intent = pi?.Metadata?["intent"];

            if (intent == "wallet_topup" && Guid.TryParse(walletId, out var wid))
            {
                // Idempotency check
                var exists = await _db.WalletTransactions.AnyAsync(t => t.ExternalRef == pi.Id);
                if (!exists)
                {
                    long cents = pi.AmountReceived > 0 ? pi.AmountReceived : pi.Amount;
                    decimal amount = cents / 100m;

                    _db.WalletTransactions.Add(new WalletTransaction
                    {
                        WalletId = wid,
                        Amount = amount,
                        Currency = pi.Currency,
                        StripeObjectId = pi.Id,
                        ExternalRef = pi.Id,
                        Description = "ACH/Card top-up via Stripe Checkout",
                        CreatedUtc = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();

                    _logger.LogInformation($"Wallet {wid} topped up with {amount} {pi.Currency}");
                }
            }
        }

        private async Task HandlePaymentIntentFailed(Event stripeEvent)
        {
            var pi = stripeEvent.Data.Object as PaymentIntent;
            _logger.LogWarning($"Payment intent failed: {pi?.Id}");

            // TODO: Send notification to user
            // var customerId = pi?.CustomerId;
            // await SendPaymentFailedNotification(customerId);
        }

        #endregion

        #region Subscription Handlers

        private async Task HandleSubscriptionCreated(Event stripeEvent)
        {
            var subscription = stripeEvent.Data.Object as Stripe.Subscription;
            if (subscription == null) return;

            _logger.LogInformation($"Subscription created: {subscription.Id}");

            try
            {
                await _subscriptionService.SyncSubscriptionFromStripeAsync(subscription.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync new subscription {subscription.Id}");
            }
        }

        private async Task HandleSubscriptionUpdated(Event stripeEvent)
        {
            var subscription = stripeEvent.Data.Object as Stripe.Subscription;
            if (subscription == null) return;

            _logger.LogInformation($"Subscription updated: {subscription.Id}");

            try
            {
                await _subscriptionService.SyncSubscriptionFromStripeAsync(subscription.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync updated subscription {subscription.Id}");
            }
        }

        private async Task HandleSubscriptionDeleted(Event stripeEvent)
        {
            var subscription = stripeEvent.Data.Object as Stripe.Subscription;
            if (subscription == null) return;

            _logger.LogInformation($"Subscription deleted: {subscription.Id}");

            try
            {
                await _subscriptionService.SyncSubscriptionFromStripeAsync(subscription.Id);

                // TODO: Send cancellation confirmation email
                // await SendSubscriptionCanceledNotification(subscription.CustomerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync deleted subscription {subscription.Id}");
            }
        }

        private async Task HandleSubscriptionTrialWillEnd(Event stripeEvent)
        {
            var subscription = stripeEvent.Data.Object as Stripe.Subscription;
            if (subscription == null) return;

            _logger.LogInformation($"Subscription trial ending soon: {subscription.Id}");

            var features = _appSettings.Subscriptions;
            if (features?.EnableEmailNotifications == true)
            {
                // TODO: Send trial ending notification
                // var daysUntilEnd = features.TrialEndingNotificationDays;
                // await SendTrialEndingNotification(subscription.CustomerId, subscription.TrialEnd);
            }
        }

        private async Task HandleSubscriptionPaused(Event stripeEvent)
        {
            var subscription = stripeEvent.Data.Object as Stripe.Subscription;
            if (subscription == null) return;

            _logger.LogInformation($"Subscription paused: {subscription.Id}");

            try
            {
                await _subscriptionService.SyncSubscriptionFromStripeAsync(subscription.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync paused subscription {subscription.Id}");
            }
        }

        private async Task HandleSubscriptionResumed(Event stripeEvent)
        {
            var subscription = stripeEvent.Data.Object as Stripe.Subscription;
            if (subscription == null) return;

            _logger.LogInformation($"Subscription resumed: {subscription.Id}");

            try
            {
                await _subscriptionService.SyncSubscriptionFromStripeAsync(subscription.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync resumed subscription {subscription.Id}");
            }
        }

        #endregion

        #region Invoice Handlers

        private async Task HandleInvoiceCreated(Event stripeEvent)
        {
            var invoice = stripeEvent.Data.Object as Stripe.Invoice;
            if (invoice == null) return;

            _logger.LogInformation($"Invoice created: {invoice.Id}");

            try
            {
                await _invoiceService.SyncInvoiceFromStripeAsync(invoice.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync new invoice {invoice.Id}");
            }
        }

        private async Task HandleInvoiceFinalized(Event stripeEvent)
        {
            var invoice = stripeEvent.Data.Object as Stripe.Invoice;
            if (invoice == null) return;

            _logger.LogInformation($"Invoice finalized: {invoice.Id}");

            try
            {
                await _invoiceService.SyncInvoiceFromStripeAsync(invoice.Id);

                // TODO: Send invoice email to customer
                // await SendInvoiceEmail(invoice.CustomerId, invoice.HostedInvoiceUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync finalized invoice {invoice.Id}");
            }
        }

        private async Task HandleInvoicePaid(Event stripeEvent)
        {
            var invoice = stripeEvent.Data.Object as Stripe.Invoice;
            if (invoice == null) return;

            _logger.LogInformation($"Invoice paid: {invoice.Id}");

            try
            {
                await _invoiceService.SyncInvoiceFromStripeAsync(invoice.Id);

                // Update subscription if this was a subscription invoice
                // SubscriptionId property doesn't exist directly in older SDK - would be Subscription object
                // Skip subscription sync for now unless Subscription is expanded

                // TODO: Send payment receipt
                // await SendPaymentReceipt(invoice.CustomerId, invoice.InvoicePdf);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync paid invoice {invoice.Id}");
            }
        }

        private async Task HandleInvoicePaymentFailed(Event stripeEvent)
        {
            var invoice = stripeEvent.Data.Object as Stripe.Invoice;
            if (invoice == null) return;

            _logger.LogWarning($"Invoice payment failed: {invoice.Id}");

            try
            {
                await _invoiceService.SyncInvoiceFromStripeAsync(invoice.Id);

                var features = _appSettings.Subscriptions;
                if (features?.EnableEmailNotifications == true)
                {
                    // TODO: Send payment failed notification
                    // await SendPaymentFailedNotification(invoice.CustomerId, invoice.HostedInvoiceUrl);
                }

                // Update subscription status if applicable
                // SubscriptionId property doesn't exist directly in older SDK - would be Subscription object
                // Skip subscription sync for now unless Subscription is expanded
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync failed invoice {invoice.Id}");
            }
        }

        private async Task HandleInvoicePaymentActionRequired(Event stripeEvent)
        {
            var invoice = stripeEvent.Data.Object as Stripe.Invoice;
            if (invoice == null) return;

            _logger.LogInformation($"Invoice requires payment action: {invoice.Id}");

            try
            {
                await _invoiceService.SyncInvoiceFromStripeAsync(invoice.Id);

                // TODO: Send notification requiring user action
                // await SendPaymentActionRequiredNotification(invoice.CustomerId, invoice.HostedInvoiceUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync invoice requiring action {invoice.Id}");
            }
        }

        private async Task HandleInvoiceUpcoming(Event stripeEvent)
        {
            var invoice = stripeEvent.Data.Object as Stripe.Invoice;
            if (invoice == null) return;

            _logger.LogInformation($"Upcoming invoice: {invoice.Id} for customer {invoice.CustomerId}");

            // This is a preview of what will be charged - typically 7 days before billing
            // TODO: Send upcoming charge notification
            // await SendUpcomingInvoiceNotification(invoice.CustomerId, invoice.Total / 100m, invoice.Currency);
        }

        #endregion

        #region Payment Method Handlers

        private async Task HandlePaymentMethodDetached(Event stripeEvent)
        {
            var paymentMethod = stripeEvent.Data.Object as PaymentMethod;
            if (paymentMethod == null) return;

            _logger.LogInformation($"Payment method detached: {paymentMethod.Id}");

            // Mark as archived in our database
            var walletPaymentMethod = await _db.WalletPaymentMethods
                .FirstOrDefaultAsync(pm => pm.PaymentMethodId == paymentMethod.Id);

            if (walletPaymentMethod != null)
            {
                walletPaymentMethod.Archived = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        #endregion
    }
}
