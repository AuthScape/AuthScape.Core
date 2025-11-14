using IDP.Models.IdentityServer;
using IDP.Services.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuthScape.Models.Authentication;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class SSOProvidersModel : PageModel
    {
        private readonly ISSOProviderService ssoProviderService;

        public SSOProvidersModel(ISSOProviderService ssoProviderService)
        {
            this.ssoProviderService = ssoProviderService;
        }

        public List<SSOProviderDto> Providers { get; set; } = new();

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Providers = await ssoProviderService.GetAllProvidersAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostToggleAsync(int providerType, bool enabled)
        {
            try
            {
                await ssoProviderService.ToggleProviderAsync((ThirdPartyAuthenticationType)providerType, enabled);
                SuccessMessage = $"Provider {(enabled ? "enabled" : "disabled")} successfully";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Error toggling provider: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int providerType)
        {
            try
            {
                await ssoProviderService.DeleteProviderConfigurationAsync((ThirdPartyAuthenticationType)providerType);
                SuccessMessage = "Provider configuration deleted successfully";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Error deleting provider configuration: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}
