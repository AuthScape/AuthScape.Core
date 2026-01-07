using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuthScape.Models.PaymentGateway.Plans;
using AuthScape.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models;
using Services.Context;
using Services.Database;
using Stripe;

namespace AuthScape.Services.Subscription
{
    public class SubscriptionPlanService : ISubscriptionPlanService
    {
        private readonly DatabaseContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppSettings _appSettings;

        public SubscriptionPlanService(
            DatabaseContext context,
            UserManager<AppUser> userManager,
            IOptions<AppSettings> appSettings)
        {
            _context = context;
            _userManager = userManager;
            _appSettings = appSettings.Value;

            // Set Stripe API key
            StripeConfiguration.ApiKey = _appSettings.Stripe?.SecretKey;
        }

        public async Task<List<SubscriptionPlanDto>> GetAllPlansAsync(bool includeInactive = false)
        {
            var query = _context.SubscriptionPlans
                .Include(sp => sp.AllowedRoles)
                .AsQueryable();

            if (!includeInactive)
                query = query.Where(sp => sp.IsActive);

            var plans = await query
                .OrderBy(sp => sp.SortOrder)
                .ThenBy(sp => sp.Name)
                .ToListAsync();

            // Get role names for display
            var roleIds = plans.SelectMany(p => p.AllowedRoles.Select(r => r.RoleId)).Distinct().ToList();
            var roles = await _context.Roles.Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name ?? "Unknown");

            return plans.Select(p => MapToDto(p, roles)).ToList();
        }

        public async Task<SubscriptionPlanDto?> GetPlanByIdAsync(Guid id)
        {
            var plan = await _context.SubscriptionPlans
                .Include(sp => sp.AllowedRoles)
                .FirstOrDefaultAsync(sp => sp.Id == id);

            if (plan == null) return null;

            var roleIds = plan.AllowedRoles.Select(r => r.RoleId).ToList();
            var roles = await _context.Roles.Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name ?? "Unknown");

            return MapToDto(plan, roles);
        }

        public async Task<SubscriptionPlanDto?> GetPlanByStripePriceIdAsync(string stripePriceId)
        {
            var plan = await _context.SubscriptionPlans
                .Include(sp => sp.AllowedRoles)
                .FirstOrDefaultAsync(sp => sp.StripePriceId == stripePriceId);

            if (plan == null) return null;

            var roleIds = plan.AllowedRoles.Select(r => r.RoleId).ToList();
            var roles = await _context.Roles.Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name ?? "Unknown");

            return MapToDto(plan, roles);
        }

        public async Task<Guid> CreatePlanAsync(CreateSubscriptionPlanDto dto)
        {
            var plan = new SubscriptionPlan
            {
                Name = dto.Name,
                Description = dto.Description,
                ImageUrl = dto.ImageUrl,
                StripePriceId = dto.StripePriceId,
                StripeProductId = dto.StripeProductId,
                EnableTrial = dto.EnableTrial,
                TrialPeriodDays = dto.TrialPeriodDays,
                Interval = dto.Interval,
                IntervalCount = dto.IntervalCount,
                Amount = (long)(dto.Amount * 100), // Convert to cents
                Currency = dto.Currency,
                TaxCode = dto.TaxCode,
                AllowedCountries = dto.AllowedCountries,
                IsActive = dto.IsActive,
                IsMostPopular = dto.IsMostPopular,
                IsContactUs = dto.IsContactUs,
                SortOrder = dto.SortOrder,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            // Add role restrictions
            if (dto.AllowedRoleIds.Any())
            {
                await SetAllowedRolesAsync(plan.Id, dto.AllowedRoleIds);
            }

            return plan.Id;
        }

        public async Task<bool> UpdatePlanAsync(UpdateSubscriptionPlanDto dto)
        {
            var plan = await _context.SubscriptionPlans.FindAsync(dto.Id);
            if (plan == null) return false;

            plan.Name = dto.Name;
            plan.Description = dto.Description;
            plan.ImageUrl = dto.ImageUrl;
            plan.StripePriceId = dto.StripePriceId;
            plan.StripeProductId = dto.StripeProductId;
            plan.EnableTrial = dto.EnableTrial;
            plan.TrialPeriodDays = dto.TrialPeriodDays;
            plan.Interval = dto.Interval;
            plan.IntervalCount = dto.IntervalCount;
            plan.Amount = (long)(dto.Amount * 100);
            plan.Currency = dto.Currency;
            plan.TaxCode = dto.TaxCode;
            plan.AllowedCountries = dto.AllowedCountries;
            plan.IsActive = dto.IsActive;
            plan.IsMostPopular = dto.IsMostPopular;
            plan.IsContactUs = dto.IsContactUs;
            plan.SortOrder = dto.SortOrder;
            plan.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
            await SetAllowedRolesAsync(plan.Id, dto.AllowedRoleIds);

            return true;
        }

        public async Task<bool> DeletePlanAsync(Guid id)
        {
            var plan = await _context.SubscriptionPlans.FindAsync(id);
            if (plan == null) return false;

            _context.SubscriptionPlans.Remove(plan);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleActiveAsync(Guid id)
        {
            var plan = await _context.SubscriptionPlans.FindAsync(id);
            if (plan == null) return false;

            plan.IsActive = !plan.IsActive;
            plan.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<StripePlanInfo>> GetStripeProductsAsync()
        {
            var priceService = new PriceService();
            var options = new PriceListOptions
            {
                Active = true,
                Type = "recurring",
                Limit = 100,
                Expand = new List<string> { "data.product" }
            };

            var prices = await priceService.ListAsync(options);

            return prices.Data
                .Where(p => p.Product != null && !p.Product.Deleted.GetValueOrDefault())
                .Select(p => new StripePlanInfo
                {
                    PriceId = p.Id,
                    ProductId = p.ProductId,
                    ProductName = p.Product?.Name ?? "Unknown",
                    Amount = p.UnitAmount.HasValue ? p.UnitAmount.Value / 100m : 0,
                    Currency = p.Currency ?? "usd",
                    Interval = p.Recurring?.Interval ?? "month",
                    IntervalCount = (int)(p.Recurring?.IntervalCount ?? 1),
                    TrialPeriodDays = (int?)(p.Recurring?.TrialPeriodDays),
                    Description = p.Product?.Description,
                    ImageUrl = p.Product?.Images?.FirstOrDefault()
                })
                .OrderBy(p => p.Amount)
                .ToList();
        }

        public async Task<int> SyncFromStripeAsync()
        {
            var stripePlans = await GetStripeProductsAsync();
            int syncCount = 0;

            foreach (var stripePlan in stripePlans)
            {
                var existing = await _context.SubscriptionPlans
                    .FirstOrDefaultAsync(sp => sp.StripePriceId == stripePlan.PriceId);

                if (existing != null)
                {
                    // Update existing
                    existing.StripeProductId = stripePlan.ProductId;
                    existing.StripeProductName = stripePlan.ProductName;
                    existing.LastStripeSyncAt = DateTimeOffset.UtcNow;
                    // Only update amount if it was originally synced (not manually overridden)
                    if (string.IsNullOrEmpty(existing.Name) || existing.Name == existing.StripeProductName)
                    {
                        existing.Name = stripePlan.ProductName;
                    }
                    if (!string.IsNullOrEmpty(stripePlan.ImageUrl) && string.IsNullOrEmpty(existing.ImageUrl))
                    {
                        existing.ImageUrl = stripePlan.ImageUrl;
                    }
                }
                else
                {
                    // Create new plan from Stripe
                    var newPlan = new SubscriptionPlan
                    {
                        StripePriceId = stripePlan.PriceId,
                        StripeProductId = stripePlan.ProductId,
                        StripeProductName = stripePlan.ProductName,
                        Name = stripePlan.ProductName,
                        Description = stripePlan.Description,
                        ImageUrl = stripePlan.ImageUrl,
                        Amount = (long)(stripePlan.Amount * 100),
                        Currency = stripePlan.Currency,
                        Interval = MapStripeInterval(stripePlan.Interval),
                        IntervalCount = stripePlan.IntervalCount,
                        TrialPeriodDays = stripePlan.TrialPeriodDays ?? 0,
                        EnableTrial = stripePlan.TrialPeriodDays > 0,
                        IsActive = true,
                        LastStripeSyncAt = DateTimeOffset.UtcNow,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    _context.SubscriptionPlans.Add(newPlan);
                }
                syncCount++;
            }

            await _context.SaveChangesAsync();
            return syncCount;
        }

        public async Task<bool> SetAllowedRolesAsync(Guid planId, List<long> roleIds)
        {
            // Remove existing role assignments
            var existing = await _context.SubscriptionPlanRoles
                .Where(spr => spr.SubscriptionPlanId == planId)
                .ToListAsync();
            _context.SubscriptionPlanRoles.RemoveRange(existing);

            // Add new role assignments
            foreach (var roleId in roleIds)
            {
                _context.SubscriptionPlanRoles.Add(new SubscriptionPlanRole
                {
                    SubscriptionPlanId = planId,
                    RoleId = roleId,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<SubscriptionPlanDto>> GetPlansForUserAsync(long userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return new List<SubscriptionPlanDto>();

            var userRoles = await _userManager.GetRolesAsync(user);
            return await GetPlansForRolesAsync(userRoles.ToList());
        }

        public async Task<List<SubscriptionPlanDto>> GetPlansForRolesAsync(List<string> roleNames)
        {
            // Get role IDs from names
            var roleIds = await _context.Roles
                .Where(r => roleNames.Contains(r.Name!))
                .Select(r => r.Id)
                .ToListAsync();

            var plans = await _context.SubscriptionPlans
                .Include(sp => sp.AllowedRoles)
                .Where(sp => sp.IsActive)
                .Where(sp => !sp.AllowedRoles.Any() || // No role restrictions
                             sp.AllowedRoles.Any(ar => roleIds.Contains(ar.RoleId))) // User has allowed role
                .OrderBy(sp => sp.SortOrder)
                .ThenBy(sp => sp.Name)
                .ToListAsync();

            var allRoleIds = plans.SelectMany(p => p.AllowedRoles.Select(r => r.RoleId)).Distinct().ToList();
            var roles = await _context.Roles.Where(r => allRoleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name ?? "Unknown");

            return plans.Select(p => MapToDto(p, roles)).ToList();
        }

        public async Task<List<SubscriptionPlanDto>> GetPlansForCountryAsync(string countryCode)
        {
            var plans = await _context.SubscriptionPlans
                .Include(sp => sp.AllowedRoles)
                .Where(sp => sp.IsActive)
                .Where(sp => string.IsNullOrEmpty(sp.AllowedCountries) || // No country restrictions
                             sp.AllowedCountries.Contains(countryCode))
                .OrderBy(sp => sp.SortOrder)
                .ThenBy(sp => sp.Name)
                .ToListAsync();

            var roleIds = plans.SelectMany(p => p.AllowedRoles.Select(r => r.RoleId)).Distinct().ToList();
            var roles = await _context.Roles.Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name ?? "Unknown");

            return plans.Select(p => MapToDto(p, roles)).ToList();
        }

        // Helper methods
        private SubscriptionPlanDto MapToDto(SubscriptionPlan plan, Dictionary<long, string> roleNames)
        {
            return new SubscriptionPlanDto
            {
                Id = plan.Id,
                StripeProductId = plan.StripeProductId,
                StripePriceId = plan.StripePriceId,
                StripeProductName = plan.StripeProductName,
                LastStripeSyncAt = plan.LastStripeSyncAt,
                Name = plan.Name,
                Description = plan.Description,
                ImageUrl = plan.ImageUrl,
                EnableTrial = plan.EnableTrial,
                TrialPeriodDays = plan.TrialPeriodDays,
                Interval = plan.Interval,
                IntervalCount = plan.IntervalCount,
                Amount = plan.Amount / 100m, // Convert cents to dollars
                Currency = plan.Currency,
                TaxCode = plan.TaxCode,
                AllowedCountries = plan.AllowedCountries,
                IsActive = plan.IsActive,
                IsMostPopular = plan.IsMostPopular,
                IsContactUs = plan.IsContactUs,
                SortOrder = plan.SortOrder,
                CreatedAt = plan.CreatedAt,
                UpdatedAt = plan.UpdatedAt,
                AllowedRoles = plan.AllowedRoles?.Select(ar => new RoleInfo
                {
                    Id = ar.RoleId,
                    Name = roleNames.TryGetValue(ar.RoleId, out var name) ? name : "Unknown"
                }).ToList() ?? new List<RoleInfo>()
            };
        }

        private BillingInterval MapStripeInterval(string? interval)
        {
            return interval?.ToLower() switch
            {
                "day" => BillingInterval.Daily,
                "week" => BillingInterval.Weekly,
                "month" => BillingInterval.Monthly,
                "year" => BillingInterval.Yearly,
                _ => BillingInterval.Monthly
            };
        }
    }
}
