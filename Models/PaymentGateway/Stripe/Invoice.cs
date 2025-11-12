using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.PaymentGateway.Stripe
{
    public class StripeInvoice
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        /// <summary>
        /// Stripe Invoice ID
        /// </summary>
        public string StripeInvoiceId { get; set; }

        /// <summary>
        /// Reference to the wallet
        /// </summary>
        public Guid WalletId { get; set; }

        /// <summary>
        /// Stripe Customer ID
        /// </summary>
        public string CustomerId { get; set; }

        /// <summary>
        /// Related subscription ID (if this is a subscription invoice)
        /// </summary>
        public Guid? SubscriptionId { get; set; }

        /// <summary>
        /// Stripe Subscription ID
        /// </summary>
        public string? StripeSubscriptionId { get; set; }

        /// <summary>
        /// Invoice number from Stripe
        /// </summary>
        public string? InvoiceNumber { get; set; }

        /// <summary>
        /// Invoice status
        /// </summary>
        public StripeInvoiceStatus Status { get; set; }

        /// <summary>
        /// Total amount in cents
        /// </summary>
        public decimal AmountDue { get; set; }

        /// <summary>
        /// Amount paid
        /// </summary>
        public decimal AmountPaid { get; set; }

        /// <summary>
        /// Amount remaining to be paid
        /// </summary>
        public decimal AmountRemaining { get; set; }

        /// <summary>
        /// Subtotal before discounts and taxes
        /// </summary>
        public decimal Subtotal { get; set; }

        /// <summary>
        /// Tax amount
        /// </summary>
        public decimal? Tax { get; set; }

        /// <summary>
        /// Total after tax and discounts
        /// </summary>
        public decimal Total { get; set; }

        /// <summary>
        /// Currency code
        /// </summary>
        public string Currency { get; set; } = "usd";

        /// <summary>
        /// Description of the invoice
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Applied discount/coupon
        /// </summary>
        public string? CouponId { get; set; }

        /// <summary>
        /// Discount amount
        /// </summary>
        public decimal? DiscountAmount { get; set; }

        /// <summary>
        /// When the invoice was created
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// When the invoice is due
        /// </summary>
        public DateTimeOffset? DueDate { get; set; }

        /// <summary>
        /// Billing period start
        /// </summary>
        public DateTimeOffset? PeriodStart { get; set; }

        /// <summary>
        /// Billing period end
        /// </summary>
        public DateTimeOffset? PeriodEnd { get; set; }

        /// <summary>
        /// When the invoice was paid
        /// </summary>
        public DateTimeOffset? PaidAt { get; set; }

        /// <summary>
        /// When payment was attempted
        /// </summary>
        public DateTimeOffset? AttemptedAt { get; set; }

        /// <summary>
        /// Number of payment attempts
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Next payment attempt date
        /// </summary>
        public DateTimeOffset? NextPaymentAttempt { get; set; }

        /// <summary>
        /// When the invoice was finalized
        /// </summary>
        public DateTimeOffset? FinalizedAt { get; set; }

        /// <summary>
        /// Payment method used
        /// </summary>
        public string? PaymentMethodId { get; set; }

        /// <summary>
        /// Stripe Charge ID
        /// </summary>
        public string? ChargeId { get; set; }

        /// <summary>
        /// Stripe Payment Intent ID
        /// </summary>
        public string? PaymentIntentId { get; set; }

        /// <summary>
        /// URL to the hosted invoice page
        /// </summary>
        public string? HostedInvoiceUrl { get; set; }

        /// <summary>
        /// URL to the invoice PDF
        /// </summary>
        public string? InvoicePdfUrl { get; set; }

        /// <summary>
        /// Whether this invoice has been automatically charged
        /// </summary>
        public bool AutoAdvance { get; set; } = true;

        /// <summary>
        /// Collection method (charge_automatically or send_invoice)
        /// </summary>
        public string? CollectionMethod { get; set; }

        /// <summary>
        /// Billing reason (subscription_create, subscription_cycle, manual, etc.)
        /// </summary>
        public string? BillingReason { get; set; }

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
        public Subscription? Subscription { get; set; }
        public ICollection<StripeInvoiceLineItem> LineItems { get; set; }
    }

    public class StripeInvoiceLineItem
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        /// <summary>
        /// Stripe Line Item ID
        /// </summary>
        public string? StripeLineItemId { get; set; }

        /// <summary>
        /// Parent invoice
        /// </summary>
        public Guid StripeInvoiceId { get; set; }

        /// <summary>
        /// Line item type (e.g., subscription, invoiceitem)
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Description of the line item
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Quantity
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
        /// Whether this is a proration
        /// </summary>
        public bool Proration { get; set; }

        /// <summary>
        /// Billing period start
        /// </summary>
        public DateTimeOffset? PeriodStart { get; set; }

        /// <summary>
        /// Billing period end
        /// </summary>
        public DateTimeOffset? PeriodEnd { get; set; }

        /// <summary>
        /// Related price ID
        /// </summary>
        public string? PriceId { get; set; }

        /// <summary>
        /// Related subscription ID
        /// </summary>
        public string? SubscriptionId { get; set; }

        /// <summary>
        /// Related subscription item ID
        /// </summary>
        public string? SubscriptionItemId { get; set; }

        /// <summary>
        /// Metadata stored as JSON
        /// </summary>
        public string? Metadata { get; set; }

        // Navigation property
        public StripeInvoice StripeInvoice { get; set; }
    }

    public enum StripeInvoiceStatus
    {
        /// <summary>
        /// Invoice is in draft state
        /// </summary>
        Draft = 1,

        /// <summary>
        /// Invoice is open and awaiting payment
        /// </summary>
        Open = 2,

        /// <summary>
        /// Invoice has been paid
        /// </summary>
        Paid = 3,

        /// <summary>
        /// Invoice is uncollectible
        /// </summary>
        Uncollectible = 4,

        /// <summary>
        /// Invoice has been voided
        /// </summary>
        Void = 5,

        /// <summary>
        /// Invoice is deleted
        /// </summary>
        Deleted = 6
    }
}
