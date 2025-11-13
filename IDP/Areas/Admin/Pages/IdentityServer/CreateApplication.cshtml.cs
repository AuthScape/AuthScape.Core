using IDP.Models.IdentityServer;
using IDP.Services.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class CreateApplicationModel : PageModel
    {
        private readonly IIdentityServerService identityServerService;

        public CreateApplicationModel(IIdentityServerService identityServerService)
        {
            this.identityServerService = identityServerService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            public ClientType ClientType { get; set; }

            [Required]
            [Display(Name = "Display Name")]
            public string DisplayName { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Client ID")]
            [RegularExpression(@"^[a-zA-Z0-9_\-\.]+$", ErrorMessage = "Client ID can only contain letters, numbers, underscores, hyphens, and dots")]
            public string ClientId { get; set; } = string.Empty;

            public string? Description { get; set; }

            public List<string> RedirectUris { get; set; } = new();

            public List<string> PostLogoutRedirectUris { get; set; } = new();

            public List<string> AllowedScopes { get; set; } = new();
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Clean up empty URIs and scopes
                var redirectUris = Input.RedirectUris?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? new List<string>();
                var logoutUris = Input.PostLogoutRedirectUris?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? new List<string>();
                var scopes = Input.AllowedScopes?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();

                var createDto = new ApplicationCreateDto
                {
                    ClientType = Input.ClientType,
                    DisplayName = Input.DisplayName,
                    ClientId = Input.ClientId,
                    Description = Input.Description,
                    RedirectUris = redirectUris,
                    PostLogoutRedirectUris = logoutUris,
                    AllowedScopes = scopes
                };

                var (clientSecret, applicationId) = await identityServerService.CreateApplicationAsync(createDto);

                // Store client secret in TempData to show on success page
                TempData["ClientSecret"] = clientSecret;
                TempData["ClientId"] = Input.ClientId;
                TempData["DisplayName"] = Input.DisplayName;
                TempData["ApplicationId"] = applicationId;

                return RedirectToPage("/IdentityServer/ApplicationCreated", new { area = "Admin" });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An error occurred while creating the application. Please try again.");
                return Page();
            }
        }
    }
}
