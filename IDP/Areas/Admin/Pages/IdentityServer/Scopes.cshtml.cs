using IDP.Models.IdentityServer;
using IDP.Services.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class ScopesModel : PageModel
    {
        private readonly IIdentityServerService identityServerService;

        public ScopesModel(IIdentityServerService identityServerService)
        {
            this.identityServerService = identityServerService;
        }

        public List<ScopeDto> Scopes { get; set; } = new();

        public async Task OnGetAsync()
        {
            Scopes = await identityServerService.GetAllScopesAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync(string scopeName, string scopeDisplayName, string? scopeDescription)
        {
            try
            {
                var createDto = new ScopeCreateDto
                {
                    Name = scopeName,
                    DisplayName = scopeDisplayName,
                    Description = scopeDescription
                };

                await identityServerService.CreateScopeAsync(createDto);
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Failed to create scope: {ex.Message}");
                await OnGetAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostEditAsync(string scopeId, string scopeName, string scopeDisplayName, string? scopeDescription)
        {
            try
            {
                var updateDto = new ScopeUpdateDto
                {
                    Id = scopeId,
                    DisplayName = scopeDisplayName,
                    Description = scopeDescription
                };

                await identityServerService.UpdateScopeAsync(updateDto);
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Failed to update scope: {ex.Message}");
                await OnGetAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(string scopeId)
        {
            try
            {
                await identityServerService.DeleteScopeAsync(scopeId);
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Failed to delete scope: {ex.Message}");
                await OnGetAsync();
                return Page();
            }
        }
    }
}
