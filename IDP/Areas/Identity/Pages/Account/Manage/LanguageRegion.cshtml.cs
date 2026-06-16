using AuthScape.Models.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace mvcTest.Areas.Identity.Pages.Account.Manage
{
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Localization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;

    public class LanguageRegionModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;

        public LanguageRegionModel(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty] public InputModel Input { get; set; } = new();
        public List<LocaleOption> LocaleOptions { get; set; } = new();

        public class InputModel
        {
            [Required] public string Culture { get; set; } = "en-US";   // single dropdown
            public string? Country { get; set; }                        // auto from culture
            public string? TimeZoneId { get; set; }                     // browser tz (IANA)
        }

        public class LocaleOption
        {
            public string Culture { get; set; } = "";
            public string Display { get; set; } = "";
            public string Country { get; set; } = "";
            public string Currency { get; set; } = ""; // ISO-4217, e.g., GBP
        }

        public async Task<IActionResult> OnGet()
        {
            BuildLocaleOptions();

            var user = await _userManager.GetUserAsync(User);

            // Preselect saved culture if present
            Input.Culture = string.IsNullOrWhiteSpace(user?.Culture)
                ? CultureInfo.CurrentUICulture.Name
                : user!.Culture;

            // Fill hidden fields from the chosen culture
            var ri = RegionInfoFromCulture(Input.Culture);
            Input.Country = ri?.TwoLetterISORegionName ?? "US";

            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            BuildLocaleOptions();

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Derive region/currency from selected culture
            var ri = RegionInfoFromCulture(Input.Culture);
            var country = ri?.TwoLetterISORegionName ?? "US";

            // Persist to user (adapt to your schema)
            user.Culture = Input.Culture;
            user.Country = country;
            user.TimeZoneId = string.IsNullOrWhiteSpace(Input.TimeZoneId) ? user.TimeZoneId : Input.TimeZoneId;

            await _userManager.UpdateAsync(user);

            // Set the culture cookie for immediate effect
            var culture = new RequestCulture(Input.Culture);
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(culture),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });

            TempData["Toast"] = "Language & Region updated.";
            return RedirectToPage(); // PRG
        }

        private void BuildLocaleOptions()
        {
            // Use all specific cultures (or filter to your supported list)
            var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.DisplayName)
                .ToList();

            LocaleOptions = new List<LocaleOption>(capacity: cultures.Count);

            foreach (var c in cultures)
            {
                var ri = RegionInfoFromCulture(c.Name);
                if (ri == null) continue;

                // Example display: "English (United Kingdom) — GBP"
                var isoCcy = ri.ISOCurrencySymbol; // e.g., GBP
                var display = $"{c.DisplayName} — {isoCcy}";

                LocaleOptions.Add(new LocaleOption
                {
                    Culture = c.Name,                  // e.g., "en-GB"
                    Display = display,                 // nice label
                    Country = ri.TwoLetterISORegionName, // "GB"
                    Currency = isoCcy                  // "GBP"
                });
            }
        }

        private static RegionInfo? RegionInfoFromCulture(string cultureName)
        {
            try
            {
                var ci = CultureInfo.GetCultureInfo(cultureName);
                return new RegionInfo(ci.LCID); // tie region to that culture (correct currency)
            }
            catch { return null; }
        }
    }

}
