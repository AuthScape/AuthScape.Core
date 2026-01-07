using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.PaymentGateway
{
    public class PromoCode
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        // ===== Display Info =====
        /// <summary>
        /// Internal name for admin reference (e.g., "Summer 2026 Sale")
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Admin description/notes about this promo code
        /// </summary>
        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>
        /// The actual promo code that users enter (e.g., "SUMMER20")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        // ===== Stripe Integration =====
        /// <summary>
        /// Stripe Coupon ID (e.g., "coupon_xxx")
        /// </summary>
        [MaxLength(100)]
        public string? StripeCouponId { get; set; }

        /// <summary>
        /// Stripe Promotion Code ID (e.g., "promo_xxx")
        /// </summary>
        [MaxLength(100)]
        public string? StripePromotionCodeId { get; set; }

        /// <summary>
        /// Last sync timestamp with Stripe
        /// </summary>
        public DateTimeOffset? LastStripeSyncAt { get; set; }

        // ===== Discount Configuration =====
        /// <summary>
        /// Type of discount (Percentage or FixedAmount)
        /// </summary>
        public PromoCodeType DiscountType { get; set; } = PromoCodeType.Percentage;

        /// <summary>
        /// Discount value (20 for 20% or 20.00 for $20 off)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        /// <summary>
        /// Currency for fixed amount discounts (e.g., "usd")
        /// </summary>
        [MaxLength(3)]
        public string Currency { get; set; } = "usd";

        // ===== Duration =====
        /// <summary>
        /// How long the discount applies (Once, Repeating, Forever)
        /// </summary>
        public PromoDuration Duration { get; set; } = PromoDuration.Once;

        /// <summary>
        /// Number of months for Repeating duration
        /// </summary>
        public int? DurationInMonths { get; set; }

        // ===== Usage Limits =====
        /// <summary>
        /// Maximum total redemptions allowed (null = unlimited)
        /// </summary>
        public int? MaxRedemptions { get; set; }

        /// <summary>
        /// Current number of times this code has been redeemed
        /// </summary>
        public int TimesRedeemed { get; set; } = 0;

        /// <summary>
        /// Maximum redemptions per customer (null = unlimited)
        /// </summary>
        public int? MaxRedemptionsPerCustomer { get; set; }

        // ===== Validity Period =====
        /// <summary>
        /// Date when promo code becomes valid (null = immediately)
        /// </summary>
        public DateTimeOffset? StartsAt { get; set; }

        /// <summary>
        /// Date when promo code expires (null = never)
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        // ===== Scope Restrictions =====
        /// <summary>
        /// Who can use this code (All, User, Company, Location)
        /// </summary>
        public PromoCodeScope Scope { get; set; } = PromoCodeScope.All;

        /// <summary>
        /// Specific user ID if Scope = User
        /// </summary>
        public long? RestrictedToUserId { get; set; }

        /// <summary>
        /// Specific company ID if Scope = Company
        /// </summary>
        public long? RestrictedToCompanyId { get; set; }

        /// <summary>
        /// Specific location ID if Scope = Location
        /// </summary>
        public long? RestrictedToLocationId { get; set; }

        // ===== Product/Service Restrictions =====
        /// <summary>
        /// What payment types this code applies to
        /// </summary>
        public PromoCodeAppliesTo AppliesTo { get; set; } = PromoCodeAppliesTo.All;

        /// <summary>
        /// Comma-separated Guid list of applicable subscription plan IDs (null = all plans)
        /// </summary>
        [MaxLength(2000)]
        public string? ApplicablePlanIds { get; set; }

        /// <summary>
        /// Comma-separated Guid list of applicable product IDs (null = all products)
        /// </summary>
        [MaxLength(2000)]
        public string? ApplicableProductIds { get; set; }

        // ===== Trial Integration =====
        /// <summary>
        /// Whether this promo code extends the trial period
        /// </summary>
        public bool ExtendsTrialDays { get; set; } = false;

        /// <summary>
        /// Additional trial days to add when using this code
        /// </summary>
        public int AdditionalTrialDays { get; set; } = 0;

        // ===== Minimum Requirements =====
        /// <summary>
        /// Minimum order amount required (null = no minimum)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinimumAmount { get; set; }

        // ===== Status =====
        /// <summary>
        /// Whether this promo code is active and can be used
        /// </summary>
        public bool IsActive { get; set; } = true;

        // ===== Audit Fields =====
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; }

        /// <summary>
        /// User ID who created this promo code
        /// </summary>
        public long? CreatedByUserId { get; set; }

        // ===== Helper Properties =====
        /// <summary>
        /// Gets a list of applicable plan IDs
        /// </summary>
        [NotMapped]
        public List<Guid> ApplicablePlanIdList => string.IsNullOrEmpty(ApplicablePlanIds)
            ? new List<Guid>()
            : ApplicablePlanIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Guid.TryParse(s.Trim(), out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

        /// <summary>
        /// Gets a list of applicable product IDs
        /// </summary>
        [NotMapped]
        public List<Guid> ApplicableProductIdList => string.IsNullOrEmpty(ApplicableProductIds)
            ? new List<Guid>()
            : ApplicableProductIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Guid.TryParse(s.Trim(), out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

        /// <summary>
        /// Gets the formatted discount display string (e.g., "20% off" or "$20.00 off")
        /// </summary>
        [NotMapped]
        public string DiscountDisplay => DiscountType == PromoCodeType.Percentage
            ? $"{DiscountValue:0.##}% off"
            : $"${DiscountValue:0.00} off";

        /// <summary>
        /// Returns true if the promo code is currently valid (active, within dates, not maxed out)
        /// </summary>
        [NotMapped]
        public bool IsCurrentlyValid
        {
            get
            {
                if (!IsActive) return false;
                var now = DateTimeOffset.UtcNow;
                if (StartsAt.HasValue && now < StartsAt.Value) return false;
                if (ExpiresAt.HasValue && now > ExpiresAt.Value) return false;
                if (MaxRedemptions.HasValue && TimesRedeemed >= MaxRedemptions.Value) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// Type of discount applied by the promo code
    /// </summary>
    public enum PromoCodeType
    {
        /// <summary>
        /// Percentage off (e.g., 20% off)
        /// </summary>
        Percentage = 1,

        /// <summary>
        /// Fixed amount off (e.g., $20 off)
        /// </summary>
        FixedAmount = 2
    }

    /// <summary>
    /// How long the discount applies to subscriptions
    /// </summary>
    public enum PromoDuration
    {
        /// <summary>
        /// Apply discount to first invoice only
        /// </summary>
        Once = 1,

        /// <summary>
        /// Apply discount for a specified number of months
        /// </summary>
        Repeating = 2,

        /// <summary>
        /// Apply discount forever (all future invoices)
        /// </summary>
        Forever = 3
    }

    /// <summary>
    /// Who can use this promo code
    /// </summary>
    public enum PromoCodeScope
    {
        /// <summary>
        /// Anyone can use this code
        /// </summary>
        All = 1,

        /// <summary>
        /// Only a specific user can use this code
        /// </summary>
        User = 2,

        /// <summary>
        /// Only users in a specific company can use this code
        /// </summary>
        Company = 3,

        /// <summary>
        /// Only users in a specific location can use this code
        /// </summary>
        Location = 4
    }

    /// <summary>
    /// What payment types this promo code applies to
    /// </summary>
    public enum PromoCodeAppliesTo
    {
        /// <summary>
        /// All payment types (subscriptions, products, services)
        /// </summary>
        All = 1,

        /// <summary>
        /// Only subscription plans
        /// </summary>
        Subscriptions = 2,

        /// <summary>
        /// Only one-time product purchases
        /// </summary>
        Products = 3,

        /// <summary>
        /// Only service purchases
        /// </summary>
        Services = 4
    }
}
