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
    public class EditApplicationModel : PageModel
    {
        private readonly IIdentityServerService identityServerService;

        public EditApplicationModel(IIdentityServerService identityServerService)
        {
            this.identityServerService = identityServerService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public ApplicationDetailsDto Application { get; set; }

        public List<ScopeDto> AvailableScopes { get; set; } = new();

        public class InputModel
        {
            public string Id { get; set; }

            [Required]
            public string DisplayName { get; set; }

            public string Description { get; set; }

            public List<string> RedirectUris { get; set; } = new List<string>();

            public List<string> PostLogoutRedirectUris { get; set; } = new List<string>();

            public List<string> AllowedScopes { get; set; } = new List<string>();

            public bool AllowOfflineAccess { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            Application = await identityServerService.GetApplicationDetailsAsync(id);

            if (Application == null)
            {
                return NotFound();
            }

            // Load available scopes
            AvailableScopes = await identityServerService.GetAllScopesAsync();

            // Populate Input from Application
            Input = new InputModel
            {
                Id = Application.Id,
                DisplayName = Application.DisplayName,
                Description = Application.Description,
                RedirectUris = Application.RedirectUris?.ToList() ?? new List<string>(),
                PostLogoutRedirectUris = Application.PostLogoutRedirectUris?.ToList() ?? new List<string>(),
                AllowedScopes = Application.Permissions?
                    .Where(p => p.StartsWith("scp:"))
                    .Select(p => p.Substring(4))
                    .ToList() ?? new List<string>(),
                AllowOfflineAccess = Application.AllowOfflineAccess
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Application = await identityServerService.GetApplicationDetailsAsync(Input.Id);
                AvailableScopes = await identityServerService.GetAllScopesAsync();
                return Page();
            }

            try
            {
                // Parse URIs from form
                var redirectUris = Input.RedirectUris?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? new List<string>();
                var logoutUris = Input.PostLogoutRedirectUris?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? new List<string>();
                var scopes = Input.AllowedScopes?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();

                var updateDto = new ApplicationUpdateDto
                {
                    Id = Input.Id,
                    DisplayName = Input.DisplayName,
                    Description = Input.Description,
                    RedirectUris = redirectUris,
                    PostLogoutRedirectUris = logoutUris,
                    AllowedScopes = scopes,
                    AllowOfflineAccess = Input.AllowOfflineAccess
                };

                await identityServerService.UpdateApplicationAsync(updateDto);

                TempData["SuccessMessage"] = "Application updated successfully";
                return RedirectToPage("/IdentityServer/Applications", new { area = "Admin" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error updating application: {ex.Message}");
                Application = await identityServerService.GetApplicationDetailsAsync(Input.Id);
                AvailableScopes = await identityServerService.GetAllScopesAsync();
                return Page();
            }
        }
    }
}
