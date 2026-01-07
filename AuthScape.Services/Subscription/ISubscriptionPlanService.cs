using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuthScape.Models.PaymentGateway.Plans;

namespace AuthScape.Services.Subscription
{
    public interface ISubscriptionPlanService
    {
        // CRUD Operations
        Task<List<SubscriptionPlanDto>> GetAllPlansAsync(bool includeInactive = false);
        Task<SubscriptionPlanDto?> GetPlanByIdAsync(Guid id);
        Task<SubscriptionPlanDto?> GetPlanByStripePriceIdAsync(string stripePriceId);
        Task<Guid> CreatePlanAsync(CreateSubscriptionPlanDto dto);
        Task<bool> UpdatePlanAsync(UpdateSubscriptionPlanDto dto);
        Task<bool> DeletePlanAsync(Guid id);
        Task<bool> ToggleActiveAsync(Guid id);

        // Stripe Sync
        Task<List<StripePlanInfo>> GetStripeProductsAsync();
        Task<int> SyncFromStripeAsync();

        // Role Management
        Task<bool> SetAllowedRolesAsync(Guid planId, List<long> roleIds);

        // Filtered Access
        Task<List<SubscriptionPlanDto>> GetPlansForUserAsync(long userId);
        Task<List<SubscriptionPlanDto>> GetPlansForRolesAsync(List<string> roleNames);
        Task<List<SubscriptionPlanDto>> GetPlansForCountryAsync(string countryCode);
    }

    public class SubscriptionPlanDto
    {
        public Guid Id { get; set; }

        // Stripe Info
        public string? StripeProductId { get; set; }
        public string? StripePriceId { get; set; }
        public string? StripeProductName { get; set; }
        public DateTimeOffset? LastStripeSyncAt { get; set; }

        // Display Info
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }

        // Trial
        public bool EnableTrial { get; set; }
        public int TrialPeriodDays { get; set; }

        // Billing
        public BillingInterval Interval { get; set; }
        public int IntervalCount { get; set; }
        public decimal Amount { get; set; } // Converted from cents to dollars
        public string Currency { get; set; } = "usd";
        public string? TaxCode { get; set; }

        // Availability
        public string? AllowedCountries { get; set; }
        public List<string> AllowedCountryList =>
            string.IsNullOrEmpty(AllowedCountries)
                ? new List<string>()
                : AllowedCountries.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        public bool IsActive { get; set; }

        // Display Settings
        public bool IsMostPopular { get; set; }
        public bool IsContactUs { get; set; }
        public int SortOrder { get; set; }

        // Role Restrictions
        public List<RoleInfo> AllowedRoles { get; set; } = new();
        public bool IsRestrictedByRole => AllowedRoles.Any();

        // Audit
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        // Computed
        public string DisplayInterval => Interval switch
        {
            BillingInterval.Daily => IntervalCount == 1 ? "day" : $"{IntervalCount} days",
            BillingInterval.Weekly => IntervalCount == 1 ? "week" : $"{IntervalCount} weeks",
            BillingInterval.Monthly => IntervalCount == 1 ? "month" : $"{IntervalCount} months",
            BillingInterval.Quarterly => "quarter",
            BillingInterval.Yearly => IntervalCount == 1 ? "year" : $"{IntervalCount} years",
            _ => "unknown"
        };

        public string FormattedPrice => $"${Amount:F2} / {DisplayInterval}";
    }

    public class CreateSubscriptionPlanDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? StripePriceId { get; set; }
        public string? StripeProductId { get; set; }
        public bool EnableTrial { get; set; }
        public int TrialPeriodDays { get; set; }
        public BillingInterval Interval { get; set; }
        public int IntervalCount { get; set; } = 1;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "usd";
        public string? TaxCode { get; set; }
        public string? AllowedCountries { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsMostPopular { get; set; }
        public bool IsContactUs { get; set; }
        public int SortOrder { get; set; }
        public List<long> AllowedRoleIds { get; set; } = new();
    }

    public class UpdateSubscriptionPlanDto : CreateSubscriptionPlanDto
    {
        public Guid Id { get; set; }
    }

    public class RoleInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class StripePlanInfo
    {
        public string PriceId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "usd";
        public string Interval { get; set; } = "month";
        public int IntervalCount { get; set; } = 1;
        public int? TrialPeriodDays { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
    }
}
