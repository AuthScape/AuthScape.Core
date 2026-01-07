using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.PaymentGateway.Plans
{
    public class SubscriptionPlan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        // ===== Stripe Sync Fields =====
        /// <summary>
        /// Stripe Product ID (e.g., "prod_xxx")
        /// </summary>
        [MaxLength(100)]
        public string? StripeProductId { get; set; }

        /// <summary>
        /// Stripe Price ID (e.g., "price_xxx")
        /// </summary>
        [MaxLength(100)]
        public string? StripePriceId { get; set; }

        /// <summary>
        /// Product name from Stripe (synced)
        /// </summary>
        [MaxLength(255)]
        public string? StripeProductName { get; set; }

        /// <summary>
        /// Last sync timestamp from Stripe
        /// </summary>
        public DateTimeOffset? LastStripeSyncAt { get; set; }

        // ===== Local Customization =====
        /// <summary>
        /// Display name (overrides Stripe product name if set)
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Plan description (overrides Stripe description if set)
        /// </summary>
        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>
        /// URL to plan image (for UI display)
        /// </summary>
        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        // ===== Trial Period =====
        /// <summary>
        /// Enable trial period for this plan
        /// </summary>
        public bool EnableTrial { get; set; } = false;

        /// <summary>
        /// Number of days for trial period
        /// </summary>
        public int TrialPeriodDays { get; set; } = 0;

        // ===== Billing Configuration =====
        /// <summary>
        /// Billing interval (monthly, yearly, weekly, etc.)
        /// </summary>
        public BillingInterval Interval { get; set; } = BillingInterval.Monthly;

        /// <summary>
        /// Interval count (e.g., 1 = every month, 3 = quarterly)
        /// </summary>
        public int IntervalCount { get; set; } = 1;

        /// <summary>
        /// Price amount in cents/smallest currency unit
        /// </summary>
        public long Amount { get; set; }

        /// <summary>
        /// Currency code (e.g., "usd", "eur")
        /// </summary>
        [MaxLength(3)]
        public string Currency { get; set; } = "usd";

        /// <summary>
        /// Stripe Tax Code ID for tax calculation
        /// </summary>
        [MaxLength(50)]
        public string? TaxCode { get; set; }

        // ===== Availability Settings =====
        /// <summary>
        /// Comma-separated list of allowed country codes (e.g., "US,CA,GB")
        /// Empty means available in all countries
        /// </summary>
        [MaxLength(500)]
        public string? AllowedCountries { get; set; }

        /// <summary>
        /// Whether this plan is active and available for purchase
        /// </summary>
        public bool IsActive { get; set; } = true;

        // ===== Display Settings =====
        /// <summary>
        /// Mark as "Most Popular" for UI highlighting
        /// </summary>
        public bool IsMostPopular { get; set; } = false;

        /// <summary>
        /// Requires user to contact sales (disables self-service purchase)
        /// </summary>
        public bool IsContactUs { get; set; } = false;

        /// <summary>
        /// Sort order for display (lower = first)
        /// </summary>
        public int SortOrder { get; set; } = 0;

        // ===== Audit Fields =====
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; }

        /// <summary>
        /// User ID who created this plan
        /// </summary>
        public long? CreatedByUserId { get; set; }

        // ===== Navigation Properties =====
        /// <summary>
        /// Role restrictions for this plan
        /// </summary>
        public virtual ICollection<SubscriptionPlanRole> AllowedRoles { get; set; } = new List<SubscriptionPlanRole>();
    }

    public enum BillingInterval
    {
        Daily = 1,
        Weekly = 2,
        Monthly = 3,
        Quarterly = 4,
        Yearly = 5
    }
}
