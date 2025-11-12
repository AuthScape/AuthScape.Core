using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.PaymentGateway.Stripe
{
    public class Subscription
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        /// <summary>
        /// Stripe Subscription ID
        /// </summary>
        public string StripeSubscriptionId { get; set; }

        /// <summary>
        /// Reference to the wallet that owns this subscription
        /// </summary>
        public Guid WalletId { get; set; }

        /// <summary>
        /// Stripe Customer ID
        /// </summary>
        public string CustomerId { get; set; }

        /// <summary>
        /// Current subscription status from Stripe
        /// </summary>
        public SubscriptionStatus Status { get; set; }

        /// <summary>
        /// Stripe Price ID for the subscription
        /// </summary>
        public string? PriceId { get; set; }

        /// <summary>
        /// Stripe Product ID
        /// </summary>
        public string? ProductId { get; set; }

        /// <summary>
        /// Product name from Stripe
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Billing amount
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Currency code (e.g., "usd")
        /// </summary>
        public string Currency { get; set; } = "usd";

        /// <summary>
        /// Billing interval (day, week, month, year)
        /// </summary>
        public string Interval { get; set; }

        /// <summary>
        /// Interval count (e.g., 1 = every month, 3 = every 3 months)
        /// </summary>
        public int IntervalCount { get; set; } = 1;

        /// <summary>
        /// When the subscription was created
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Current billing period start
        /// </summary>
        public DateTimeOffset CurrentPeriodStart { get; set; }

        /// <summary>
        /// Current billing period end
        /// </summary>
        public DateTimeOffset CurrentPeriodEnd { get; set; }

        /// <summary>
        /// When the subscription was canceled (if applicable)
        /// </summary>
        public DateTimeOffset? CanceledAt { get; set; }

        /// <summary>
        /// When the subscription will end (for cancellations at period end)
        /// </summary>
        public DateTimeOffset? EndedAt { get; set; }

        /// <summary>
        /// Trial start date
        /// </summary>
        public DateTimeOffset? TrialStart { get; set; }

        /// <summary>
        /// Trial end date
        /// </summary>
        public DateTimeOffset? TrialEnd { get; set; }

        /// <summary>
        /// Whether to cancel at period end
        /// </summary>
        public bool CancelAtPeriodEnd { get; set; }

        /// <summary>
        /// Applied coupon/promo code
        /// </summary>
        public string? CouponId { get; set; }

        /// <summary>
        /// Discount amount if applicable
        /// </summary>
        public decimal? DiscountAmount { get; set; }

        /// <summary>
        /// Default payment method for this subscription
        /// </summary>
        public string? DefaultPaymentMethodId { get; set; }

        /// <summary>
        /// Latest invoice ID
        /// </summary>
        public string? LatestInvoiceId { get; set; }

        /// <summary>
        /// Metadata stored as JSON
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// When this record was last synced from Stripe
        /// </summary>
        public DateTimeOffset LastSyncedAt { get; set; }

        // Navigation properties
        public Wallet Wallet { get; set; }
        public ICollection<SubscriptionItem> Items { get; set; }
    }

    public class SubscriptionItem
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        /// <summary>
        /// Stripe Subscription Item ID
        /// </summary>
        public string StripeSubscriptionItemId { get; set; }

        /// <summary>
        /// Parent subscription
        /// </summary>
        public Guid SubscriptionId { get; set; }

        /// <summary>
        /// Stripe Price ID
        /// </summary>
        public string PriceId { get; set; }

        /// <summary>
        /// Stripe Product ID
        /// </summary>
        public string? ProductId { get; set; }

        /// <summary>
        /// Product description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Quantity (for per-seat or usage-based billing)
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Unit amount in cents
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Currency code
        /// </summary>
        public string Currency { get; set; } = "usd";

        /// <summary>
        /// Billing type (recurring, usage, etc.)
        /// </summary>
        public string? BillingType { get; set; }

        /// <summary>
        /// Metadata stored as JSON
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// When this record was created
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        // Navigation property
        public Subscription Subscription { get; set; }
    }

    public enum SubscriptionStatus
    {
        /// <summary>
        /// The subscription is in good standing and the most recent payment was successful
        /// </summary>
        Active = 1,

        /// <summary>
        /// Payment failed or waiting for payment
        /// </summary>
        PastDue = 2,

        /// <summary>
        /// Subscription is no longer active (expired or canceled)
        /// </summary>
        Canceled = 3,

        /// <summary>
        /// Subscription has failed and is no longer being retried
        /// </summary>
        Unpaid = 4,

        /// <summary>
        /// Currently in trial period
        /// </summary>
        Trialing = 5,

        /// <summary>
        /// Subscription is incomplete and requires action
        /// </summary>
        Incomplete = 6,

        /// <summary>
        /// First payment attempt failed
        /// </summary>
        IncompleteExpired = 7,

        /// <summary>
        /// Subscription is paused
        /// </summary>
        Paused = 8
    }
}
