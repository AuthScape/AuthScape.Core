using AuthScape.Models.PaymentGateway;
using AuthScape.Services.PromoCode;
using AuthScape.Services.Subscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Services.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class PromoCodesModel : PageModel
    {
        private readonly IPromoCodeService _promoCodeService;
        private readonly ISubscriptionPlanService _planService;
        private readonly AppSettings _appSettings;

        public PromoCodesModel(
            IPromoCodeService promoCodeService,
            ISubscriptionPlanService planService,
            IOptions<AppSettings> appSettings)
        {
            _promoCodeService = promoCodeService;
            _planService = planService;
            _appSettings = appSettings.Value;
        }

        public List<PromoCodeDto> PromoCodes { get; set; } = new();
        public List<SubscriptionPlanDto> AvailablePlans { get; set; } = new();

        [TempData]
        public string SuccessMessage { get; set; } = string.Empty;

        [TempData]
        public string ErrorMessage { get; set; } = string.Empty;

        // Stats
        public int TotalCount => PromoCodes.Count;
        public int ActiveCount => PromoCodes.Count(p => p.StatusDisplay == "Active");
        public int ExpiredCount => PromoCodes.Count(p => p.StatusDisplay == "Expired");
        public int SyncedCount => PromoCodes.Count(p => p.IsSyncedToStripe);

        public async Task<IActionResult> OnGetAsync()
        {
            PromoCodes = await _promoCodeService.GetAllAsync(includeInactive: true);
            AvailablePlans = await _planService.GetAllPlansAsync(includeInactive: false);
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync(
            string name,
            string? description,
            string? code,
            bool autoGenerateCode,
            PromoCodeType discountType,
            decimal discountValue,
            string currency,
            PromoDuration duration,
            int? durationInMonths,
            int? maxRedemptions,
            int? maxRedemptionsPerCustomer,
            DateTimeOffset? startsAt,
            DateTimeOffset? expiresAt,
            PromoCodeScope scope,
            long? restrictedToUserId,
            long? restrictedToCompanyId,
            long? restrictedToLocationId,
            PromoCodeAppliesTo appliesTo,
            List<Guid>? applicablePlanIds,
            bool extendsTrialDays,
            int additionalTrialDays,
            decimal? minimumAmount,
            bool isActive)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    ErrorMessage = "Name is required";
                    return RedirectToPage();
                }

                var dto = new CreatePromoCodeDto
                {
                    Name = name,
                    Description = description,
                    Code = code,
                    AutoGenerateCode = autoGenerateCode || string.IsNullOrWhiteSpace(code),
                    DiscountType = discountType,
                    DiscountValue = discountValue,
                    Currency = currency ?? "usd",
                    Duration = duration,
                    DurationInMonths = duration == PromoDuration.Repeating ? durationInMonths : null,
                    MaxRedemptions = maxRedemptions,
                    MaxRedemptionsPerCustomer = maxRedemptionsPerCustomer,
                    StartsAt = startsAt,
                    ExpiresAt = expiresAt,
                    Scope = scope,
                    RestrictedToUserId = restrictedToUserId,
                    RestrictedToCompanyId = restrictedToCompanyId,
                    RestrictedToLocationId = restrictedToLocationId,
                    AppliesTo = appliesTo,
                    ApplicablePlanIds = applicablePlanIds,
                    ExtendsTrialDays = extendsTrialDays,
                    AdditionalTrialDays = additionalTrialDays,
                    MinimumAmount = minimumAmount,
                    IsActive = isActive
                };

                await _promoCodeService.CreateAsync(dto);
                SuccessMessage = $"Promo code '{name}' created and synced to Stripe";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error creating promo code: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            Guid id,
            string name,
            string? description,
            string? code,
            PromoCodeType discountType,
            decimal discountValue,
            string currency,
            PromoDuration duration,
            int? durationInMonths,
            int? maxRedemptions,
            int? maxRedemptionsPerCustomer,
            DateTimeOffset? startsAt,
            DateTimeOffset? expiresAt,
            PromoCodeScope scope,
            long? restrictedToUserId,
            long? restrictedToCompanyId,
            long? restrictedToLocationId,
            PromoCodeAppliesTo appliesTo,
            List<Guid>? applicablePlanIds,
            bool extendsTrialDays,
            int additionalTrialDays,
            decimal? minimumAmount,
            bool isActive)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    ErrorMessage = "Name is required";
                    return RedirectToPage();
                }

                var dto = new UpdatePromoCodeDto
                {
                    Id = id,
                    Name = name,
                    Description = description,
                    Code = code,
                    AutoGenerateCode = false, // Never auto-generate on update
                    DiscountType = discountType,
                    DiscountValue = discountValue,
                    Currency = currency ?? "usd",
                    Duration = duration,
                    DurationInMonths = duration == PromoDuration.Repeating ? durationInMonths : null,
                    MaxRedemptions = maxRedemptions,
                    MaxRedemptionsPerCustomer = maxRedemptionsPerCustomer,
                    StartsAt = startsAt,
                    ExpiresAt = expiresAt,
                    Scope = scope,
                    RestrictedToUserId = restrictedToUserId,
                    RestrictedToCompanyId = restrictedToCompanyId,
                    RestrictedToLocationId = restrictedToLocationId,
                    AppliesTo = appliesTo,
                    ApplicablePlanIds = applicablePlanIds,
                    ExtendsTrialDays = extendsTrialDays,
                    AdditionalTrialDays = additionalTrialDays,
                    MinimumAmount = minimumAmount,
                    IsActive = isActive
                };

                var success = await _promoCodeService.UpdateAsync(dto);
                if (success)
                    SuccessMessage = $"Promo code '{name}' updated successfully";
                else
                    ErrorMessage = "Failed to update promo code";

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating promo code: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            try
            {
                var promoCode = await _promoCodeService.GetByIdAsync(id);
                if (promoCode == null)
                {
                    ErrorMessage = "Promo code not found";
                    return RedirectToPage();
                }

                var success = await _promoCodeService.DeleteAsync(id);
                if (success)
                    SuccessMessage = $"Promo code '{promoCode.Code}' deleted successfully";
                else
                    ErrorMessage = "Failed to delete promo code";

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting promo code: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(Guid id)
        {
            try
            {
                var success = await _promoCodeService.ToggleActiveAsync(id);
                if (success)
                    SuccessMessage = "Promo code status updated";
                else
                    ErrorMessage = "Failed to update promo code status";

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error updating status: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostSyncFromStripeAsync()
        {
            try
            {
                var count = await _promoCodeService.SyncAllFromStripeAsync();
                SuccessMessage = $"Successfully synced {count} promo code(s) from Stripe";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Stripe sync failed: {ex.Message}";
            }

            return RedirectToPage();
        }

        public IActionResult OnGetGenerateCode()
        {
            var code = _promoCodeService.GenerateRandomCode();
            return new JsonResult(new { code });
        }

        // Search handlers for AJAX
        public async Task<IActionResult> OnGetSearchUsersAsync(string term)
        {
            var results = await _promoCodeService.SearchUsersAsync(term);
            return new JsonResult(results);
        }

        public async Task<IActionResult> OnGetSearchCompaniesAsync(string term)
        {
            var results = await _promoCodeService.SearchCompaniesAsync(term);
            return new JsonResult(results);
        }

        public async Task<IActionResult> OnGetSearchLocationsAsync(string term)
        {
            var results = await _promoCodeService.SearchLocationsAsync(term);
            return new JsonResult(results);
        }
    }
}
