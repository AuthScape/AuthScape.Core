using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class ApplicationCreatedModel : PageModel
    {
        public string ClientSecret { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ApplicationId { get; set; } = string.Empty;

        public IActionResult OnGet()
        {
            // Get data from TempData
            if (TempData["ClientSecret"] != null)
            {
                ClientSecret = TempData["ClientSecret"]?.ToString() ?? string.Empty;
                ClientId = TempData["ClientId"]?.ToString() ?? string.Empty;
                DisplayName = TempData["DisplayName"]?.ToString() ?? string.Empty;
                ApplicationId = TempData["ApplicationId"]?.ToString() ?? string.Empty;
            }
            else
            {
                // If no TempData, redirect back to applications list
                return RedirectToPage("/IdentityServer/Applications");
            }

            return Page();
        }
    }
}
