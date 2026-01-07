using AuthScape.Models.PaymentGateway.Plans;
using AuthScape.Services;
using AuthScape.Services.Azure.Storage;
using AuthScape.Services.Subscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Services.Database;
using Stripe;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class SubscriptionPlansModel : PageModel
    {
        private readonly ISubscriptionPlanService _planService;
        private readonly IRoleService _roleService;
        private readonly IAzureBlobStorage _blobStorage;
        private readonly AppSettings _appSettings;

        public SubscriptionPlansModel(
            ISubscriptionPlanService planService,
            IRoleService roleService,
            IAzureBlobStorage blobStorage,
            IOptions<AppSettings> appSettings)
        {
            _planService = planService;
            _roleService = roleService;
            _blobStorage = blobStorage;
            _appSettings = appSettings.Value;

            // Set Stripe API key
            StripeConfiguration.ApiKey = _appSettings.Stripe?.SecretKey;
        }

        public List<SubscriptionPlanDto> Plans { get; set; } = new();
        public List<RoleDto> AvailableRoles { get; set; } = new();
        public List<StripePlanInfo> StripePlans { get; set; } = new();
        public List<StripeTaxCode> TaxCodes { get; set; } = new();
        public List<LocaleOption> LocaleOptions { get; set; } = new();

        [TempData]
        public string SuccessMessage { get; set; } = string.Empty;

        [TempData]
        public string ErrorMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            Plans = await _planService.GetAllPlansAsync(includeInactive: true);
            AvailableRoles = await _roleService.GetAllRolesAsync();
            BuildLocaleOptions();

            try
            {
                StripePlans = await _planService.GetStripeProductsAsync();
            }
            catch
            {
                StripePlans = new List<StripePlanInfo>();
            }

            try
            {
                TaxCodes = await GetStripeTaxCodesAsync();
            }
            catch
            {
                TaxCodes = new List<StripeTaxCode>();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync(
            string name,
            string? description,
            IFormFile? imageFile,
            string? stripePriceId,
            string? stripeProductId,
            bool createStripeProduct,
            bool enableTrial,
            int trialPeriodDays,
            BillingInterval interval,
            int intervalCount,
            decimal amount,
            string currency,
            string? taxCode,
            List<string>? allowedCountries,
            bool isActive,
            int sortOrder,
            List<long>? allowedRoleIds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    ErrorMessage = "Plan name is required";
                    return RedirectToPage();
                }

                string? imageUrl = null;
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        var fileName = await _blobStorage.UploadFile(imageFile, "subscription-plans", Guid.NewGuid().ToString());
                        imageUrl = $"{_appSettings.Storage?.BaseUri}/subscription-plans/{fileName}";
                    }
                    catch (Exception ex)
                    {
                        ErrorMessage = $"Failed to upload image: {ex.Message}";
                        return RedirectToPage();
                    }
                }

                // Create Stripe product and price if requested
                if (createStripeProduct)
                {
                    try
                    {
                        var stripeResult = await CreateStripeProductAndPriceAsync(
                            name, description, imageUrl, amount, currency, interval, intervalCount, enableTrial, trialPeriodDays, taxCode);
                        stripeProductId = stripeResult.ProductId;
                        stripePriceId = stripeResult.PriceId;
                    }
                    catch (Exception ex)
                    {
                        ErrorMessage = $"Failed to create Stripe product: {ex.Message}";
                        return RedirectToPage();
                    }
                }

                var dto = new CreateSubscriptionPlanDto
                {
                    Name = name,
                    Description = description,
                    ImageUrl = imageUrl,
                    StripePriceId = stripePriceId,
                    StripeProductId = stripeProductId,
                    EnableTrial = enableTrial,
                    TrialPeriodDays = trialPeriodDays,
                    Interval = interval,
                    IntervalCount = intervalCount <= 0 ? 1 : intervalCount,
                    Amount = amount,
                    Currency = currency ?? "usd",
                    TaxCode = taxCode,
                    AllowedCountries = allowedCountries != null && allowedCountries.Any()
                        ? string.Join(",", allowedCountries)
                        : null,
                    IsActive = isActive,
                    IsMostPopular = false,
                    IsContactUs = false,
                    SortOrder = sortOrder,
                    AllowedRoleIds = allowedRoleIds ?? new List<long>()
                };

                await _planService.CreatePlanAsync(dto);
                var successMsg = $"Plan '{name}' created successfully";
                if (createStripeProduct)
                    successMsg += " and synced to Stripe";
                SuccessMessage = successMsg;

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            Guid id,
            string name,
            string? description,
            IFormFile? imageFile,
            string? existingImageUrl,
            string? stripePriceId,
            string? stripeProductId,
            bool enableTrial,
            int trialPeriodDays,
            BillingInterval interval,
            int intervalCount,
            decimal amount,
            string currency,
            string? taxCode,
            List<string>? allowedCountries,
            bool isActive,
            int sortOrder,
            List<long>? allowedRoleIds)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Plan name is required";
                return RedirectToPage();
            }

            string? imageUrl = existingImageUrl;
            if (imageFile != null && imageFile.Length > 0)
            {
                try
                {
                    var fileName = await _blobStorage.UploadFile(imageFile, "subscription-plans", Guid.NewGuid().ToString());
                    imageUrl = $"{_appSettings.Storage?.BaseUri}/subscription-plans/{fileName}";
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Failed to upload image: {ex.Message}";
                    return RedirectToPage();
                }
            }

            var dto = new UpdateSubscriptionPlanDto
            {
                Id = id,
                Name = name,
                Description = description,
                ImageUrl = imageUrl,
                StripePriceId = stripePriceId,
                StripeProductId = stripeProductId,
                EnableTrial = enableTrial,
                TrialPeriodDays = trialPeriodDays,
                Interval = interval,
                IntervalCount = intervalCount <= 0 ? 1 : intervalCount,
                Amount = amount,
                Currency = currency ?? "usd",
                TaxCode = taxCode,
                AllowedCountries = allowedCountries != null && allowedCountries.Any()
                    ? string.Join(",", allowedCountries)
                    : null,
                IsActive = isActive,
                IsMostPopular = false,
                IsContactUs = false,
                SortOrder = sortOrder,
                AllowedRoleIds = allowedRoleIds ?? new List<long>()
            };

            var success = await _planService.UpdatePlanAsync(dto);
            if (success)
                SuccessMessage = $"Plan '{name}' updated successfully";
            else
                ErrorMessage = "Failed to update plan";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            var plan = await _planService.GetPlanByIdAsync(id);
            if (plan == null)
            {
                ErrorMessage = "Plan not found";
                return RedirectToPage();
            }

            var success = await _planService.DeletePlanAsync(id);
            if (success)
                SuccessMessage = $"Plan '{plan.Name}' deleted successfully";
            else
                ErrorMessage = "Failed to delete plan";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(Guid id)
        {
            var success = await _planService.ToggleActiveAsync(id);
            if (success)
                SuccessMessage = "Plan status updated";
            else
                ErrorMessage = "Failed to update plan status";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSyncFromStripeAsync()
        {
            try
            {
                var count = await _planService.SyncFromStripeAsync();
                SuccessMessage = $"Successfully synced {count} plan(s) from Stripe";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Stripe sync failed: {ex.Message}";
            }

            return RedirectToPage();
        }

        private async Task<List<StripeTaxCode>> GetStripeTaxCodesAsync()
        {
            var taxCodeService = new TaxCodeService();
            var options = new TaxCodeListOptions { Limit = 100 };
            var taxCodes = new List<StripeTaxCode>();

            var result = await taxCodeService.ListAsync(options);
            foreach (var tc in result.Data)
            {
                taxCodes.Add(new StripeTaxCode
                {
                    Id = tc.Id,
                    Name = tc.Name,
                    Description = tc.Description
                });
            }

            return taxCodes.OrderBy(t => t.Name).ToList();
        }

        private void BuildLocaleOptions()
        {
            var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.DisplayName)
                .ToList();

            LocaleOptions = new List<LocaleOption>(capacity: cultures.Count);

            foreach (var c in cultures)
            {
                var ri = RegionInfoFromCulture(c.Name);
                if (ri == null) continue;

                LocaleOptions.Add(new LocaleOption
                {
                    Culture = c.Name,
                    Display = $"{ri.EnglishName} ({ri.TwoLetterISORegionName})",
                    CountryCode = ri.TwoLetterISORegionName,
                    CountryName = ri.EnglishName
                });
            }

            // Remove duplicates by country code
            LocaleOptions = LocaleOptions
                .GroupBy(l => l.CountryCode)
                .Select(g => g.First())
                .OrderBy(l => l.CountryName)
                .ToList();
        }

        private static RegionInfo? RegionInfoFromCulture(string cultureName)
        {
            try
            {
                var ci = CultureInfo.GetCultureInfo(cultureName);
                return new RegionInfo(ci.LCID);
            }
            catch { return null; }
        }

        private async Task<(string ProductId, string PriceId)> CreateStripeProductAndPriceAsync(
            string name,
            string? description,
            string? imageUrl,
            decimal amount,
            string currency,
            BillingInterval interval,
            int intervalCount,
            bool enableTrial,
            int trialPeriodDays,
            string? taxCode)
        {
            // Create the Product
            var productService = new ProductService();
            var productOptions = new ProductCreateOptions
            {
                Name = name,
                Description = description,
                DefaultPriceData = null, // We'll create price separately
                TaxCode = taxCode
            };

            if (!string.IsNullOrEmpty(imageUrl))
            {
                productOptions.Images = new List<string> { imageUrl };
            }

            var product = await productService.CreateAsync(productOptions);

            // Create the Price
            var priceService = new PriceService();
            var priceOptions = new PriceCreateOptions
            {
                Product = product.Id,
                UnitAmount = (long)(amount * 100), // Convert to cents
                Currency = currency.ToLower(),
                Recurring = new PriceRecurringOptions
                {
                    Interval = MapBillingIntervalToStripe(interval),
                    IntervalCount = intervalCount
                }
            };

            // Add trial period if enabled
            if (enableTrial && trialPeriodDays > 0)
            {
                priceOptions.Recurring.TrialPeriodDays = trialPeriodDays;
            }

            var price = await priceService.CreateAsync(priceOptions);

            return (product.Id, price.Id);
        }

        private static string MapBillingIntervalToStripe(BillingInterval interval)
        {
            return interval switch
            {
                BillingInterval.Daily => "day",
                BillingInterval.Weekly => "week",
                BillingInterval.Monthly => "month",
                BillingInterval.Quarterly => "month", // Stripe uses month with intervalCount=3
                BillingInterval.Yearly => "year",
                _ => "month"
            };
        }
    }

    public class StripeTaxCode
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class LocaleOption
    {
        public string Culture { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string CountryName { get; set; } = string.Empty;
    }
}
