using AuthScape.Models.PaymentGateway;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthScape.Services.PromoCode
{
    public interface IPromoCodeService
    {
        // CRUD Operations
        Task<List<PromoCodeDto>> GetAllAsync(bool includeInactive = false);
        Task<PromoCodeDto?> GetByIdAsync(Guid id);
        Task<PromoCodeDto?> GetByCodeAsync(string code);
        Task<Guid> CreateAsync(CreatePromoCodeDto dto);
        Task<bool> UpdateAsync(UpdatePromoCodeDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ToggleActiveAsync(Guid id);

        // Code Generation
        string GenerateRandomCode(int length = 8);

        // Validation
        Task<PromoCodeValidationResult> ValidateCodeAsync(
            string code,
            Guid? planId = null,
            long? userId = null,
            long? companyId = null,
            long? locationId = null,
            decimal? orderAmount = null);

        // Stripe Sync
        Task<bool> SyncToStripeAsync(Guid promoCodeId);
        Task<int> SyncAllFromStripeAsync();

        // Usage Tracking
        Task<bool> RecordRedemptionAsync(Guid promoCodeId);

        // Search for scope assignment
        Task<List<UserSearchResult>> SearchUsersAsync(string searchTerm, int limit = 10);
        Task<List<CompanySearchResult>> SearchCompaniesAsync(string searchTerm, int limit = 10);
        Task<List<LocationSearchResult>> SearchLocationsAsync(string searchTerm, int limit = 10);
    }

    public class PromoCodeDto
    {
        public Guid Id { get; set; }

        // Display Info
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Code { get; set; } = string.Empty;

        // Stripe Integration
        public string? StripeCouponId { get; set; }
        public string? StripePromotionCodeId { get; set; }
        public DateTimeOffset? LastStripeSyncAt { get; set; }
        public bool IsSyncedToStripe => !string.IsNullOrEmpty(StripeCouponId) && !string.IsNullOrEmpty(StripePromotionCodeId);

        // Discount Configuration
        public PromoCodeType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public string Currency { get; set; } = "usd";

        // Duration
        public PromoDuration Duration { get; set; }
        public int? DurationInMonths { get; set; }

        // Usage Limits
        public int? MaxRedemptions { get; set; }
        public int TimesRedeemed { get; set; }
        public int? MaxRedemptionsPerCustomer { get; set; }

        // Validity Period
        public DateTimeOffset? StartsAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }

        // Scope Restrictions
        public PromoCodeScope Scope { get; set; }
        public long? RestrictedToUserId { get; set; }
        public string? RestrictedToUserName { get; set; }
        public long? RestrictedToCompanyId { get; set; }
        public string? RestrictedToCompanyName { get; set; }
        public long? RestrictedToLocationId { get; set; }
        public string? RestrictedToLocationName { get; set; }

        // Product/Service Restrictions
        public PromoCodeAppliesTo AppliesTo { get; set; }
        public string? ApplicablePlanIds { get; set; }
        public string? ApplicableProductIds { get; set; }
        public List<Guid> ApplicablePlanIdList =>
            string.IsNullOrEmpty(ApplicablePlanIds)
                ? new List<Guid>()
                : ApplicablePlanIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Guid.TryParse(s.Trim(), out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToList();

        // Trial Integration
        public bool ExtendsTrialDays { get; set; }
        public int AdditionalTrialDays { get; set; }

        // Minimum Requirements
        public decimal? MinimumAmount { get; set; }

        // Status
        public bool IsActive { get; set; }

        // Audit
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public long? CreatedByUserId { get; set; }

        // Computed Properties
        public string DiscountDisplay => DiscountType == PromoCodeType.Percentage
            ? $"{DiscountValue:0.##}% off"
            : $"${DiscountValue:0.00} off";

        public string DurationDisplay => Duration switch
        {
            PromoDuration.Once => "First payment only",
            PromoDuration.Repeating => $"For {DurationInMonths} month(s)",
            PromoDuration.Forever => "Forever",
            _ => "Unknown"
        };

        public string ScopeDisplay => Scope switch
        {
            PromoCodeScope.All => "All users",
            PromoCodeScope.User => RestrictedToUserName ?? $"User #{RestrictedToUserId}",
            PromoCodeScope.Company => RestrictedToCompanyName ?? $"Company #{RestrictedToCompanyId}",
            PromoCodeScope.Location => RestrictedToLocationName ?? $"Location #{RestrictedToLocationId}",
            _ => "Unknown"
        };

        public string AppliesToDisplay => AppliesTo switch
        {
            PromoCodeAppliesTo.All => "All payment types",
            PromoCodeAppliesTo.Subscriptions => "Subscriptions only",
            PromoCodeAppliesTo.Products => "Products only",
            PromoCodeAppliesTo.Services => "Services only",
            _ => "Unknown"
        };

        public string UsageDisplay => MaxRedemptions.HasValue
            ? $"{TimesRedeemed} / {MaxRedemptions}"
            : $"{TimesRedeemed} (unlimited)";

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

        public string StatusDisplay
        {
            get
            {
                if (!IsActive) return "Inactive";
                var now = DateTimeOffset.UtcNow;
                if (StartsAt.HasValue && now < StartsAt.Value) return "Scheduled";
                if (ExpiresAt.HasValue && now > ExpiresAt.Value) return "Expired";
                if (MaxRedemptions.HasValue && TimesRedeemed >= MaxRedemptions.Value) return "Maxed Out";
                return "Active";
            }
        }
    }

    public class CreatePromoCodeDto
    {
        // Display Info
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Code { get; set; } // If null/empty, auto-generate
        public bool AutoGenerateCode { get; set; }

        // Discount Configuration
        public PromoCodeType DiscountType { get; set; } = PromoCodeType.Percentage;
        public decimal DiscountValue { get; set; }
        public string Currency { get; set; } = "usd";

        // Duration
        public PromoDuration Duration { get; set; } = PromoDuration.Once;
        public int? DurationInMonths { get; set; }

        // Usage Limits
        public int? MaxRedemptions { get; set; }
        public int? MaxRedemptionsPerCustomer { get; set; }

        // Validity Period
        public DateTimeOffset? StartsAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }

        // Scope Restrictions
        public PromoCodeScope Scope { get; set; } = PromoCodeScope.All;
        public long? RestrictedToUserId { get; set; }
        public long? RestrictedToCompanyId { get; set; }
        public long? RestrictedToLocationId { get; set; }

        // Product/Service Restrictions
        public PromoCodeAppliesTo AppliesTo { get; set; } = PromoCodeAppliesTo.All;
        public List<Guid>? ApplicablePlanIds { get; set; }
        public List<Guid>? ApplicableProductIds { get; set; }

        // Trial Integration
        public bool ExtendsTrialDays { get; set; }
        public int AdditionalTrialDays { get; set; }

        // Minimum Requirements
        public decimal? MinimumAmount { get; set; }

        // Status
        public bool IsActive { get; set; } = true;

        // Created By
        public long? CreatedByUserId { get; set; }
    }

    public class UpdatePromoCodeDto : CreatePromoCodeDto
    {
        public Guid Id { get; set; }
    }

    public class PromoCodeValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }

        // Promo Code Info (populated if valid)
        public Guid? PromoCodeId { get; set; }
        public string? StripePromotionCodeId { get; set; }
        public string? StripeCouponId { get; set; }
        public PromoCodeType? DiscountType { get; set; }
        public decimal? DiscountValue { get; set; }
        public string? DiscountDisplay { get; set; }
        public PromoDuration? Duration { get; set; }
        public int? DurationInMonths { get; set; }

        // Trial extension
        public bool ExtendsTrialDays { get; set; }
        public int AdditionalTrialDays { get; set; }

        // Static factory methods
        public static PromoCodeValidationResult Invalid(string errorMessage) => new()
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };

        public static PromoCodeValidationResult Valid(PromoCodeDto promo) => new()
        {
            IsValid = true,
            PromoCodeId = promo.Id,
            StripePromotionCodeId = promo.StripePromotionCodeId,
            StripeCouponId = promo.StripeCouponId,
            DiscountType = promo.DiscountType,
            DiscountValue = promo.DiscountValue,
            DiscountDisplay = promo.DiscountDisplay,
            Duration = promo.Duration,
            DurationInMonths = promo.DurationInMonths,
            ExtendsTrialDays = promo.ExtendsTrialDays,
            AdditionalTrialDays = promo.AdditionalTrialDays
        };
    }

    // Search result classes for scope assignment
    public class UserSearchResult
    {
        public long Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class CompanySearchResult
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class LocationSearchResult
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long? CompanyId { get; set; }
        public string? CompanyName { get; set; }
    }
}
