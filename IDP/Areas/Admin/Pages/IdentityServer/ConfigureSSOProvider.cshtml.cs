using AuthScape.Models.Authentication;
using IDP.Models.IdentityServer;
using IDP.Services.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Tasks;

namespace IDP.Areas.Admin.Pages.IdentityServer
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class ConfigureSSOProviderModel : PageModel
    {
        private readonly ISSOProviderService ssoProviderService;

        public ConfigureSSOProviderModel(ISSOProviderService ssoProviderService)
        {
            this.ssoProviderService = ssoProviderService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public SSOProviderDto Provider { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            public ThirdPartyAuthenticationType ProviderType { get; set; }

            public bool IsEnabled { get; set; }

            [Required(ErrorMessage = "Client ID is required")]
            public string ClientId { get; set; }

            [Required(ErrorMessage = "Client Secret is required")]
            public string ClientSecret { get; set; }

            public string Scopes { get; set; }

            // Provider-specific fields
            public string TenantId { get; set; } // Microsoft
            public string TeamId { get; set; } // Apple
            public string KeyId { get; set; } // Apple
            public string PrivateKey { get; set; } // Apple
            public string Region { get; set; } // Battle.Net
            public string EnterpriseDomain { get; set; } // GitHub
        }

        public async Task<IActionResult> OnGetAsync(int providerType)
        {
            Provider = await ssoProviderService.GetProviderAsync((ThirdPartyAuthenticationType)providerType);

            if (Provider == null)
            {
                return NotFound();
            }

            // Populate input from existing configuration
            Input = new InputModel
            {
                ProviderType = Provider.ProviderType,
                IsEnabled = Provider.IsEnabled,
                ClientId = Provider.ClientId,
                ClientSecret = Provider.ClientSecret,
                Scopes = Provider.Scopes
            };

            // Parse additional settings if they exist
            if (!string.IsNullOrEmpty(Provider.AdditionalSettings))
            {
                try
                {
                    var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(Provider.AdditionalSettings);
                    if (settings != null)
                    {
                        settings.TryGetValue("TenantId", out var tenantId);
                        settings.TryGetValue("TeamId", out var teamId);
                        settings.TryGetValue("KeyId", out var keyId);
                        settings.TryGetValue("PrivateKey", out var privateKey);
                        settings.TryGetValue("Region", out var region);
                        settings.TryGetValue("EnterpriseDomain", out var enterpriseDomain);

                        Input.TenantId = tenantId;
                        Input.TeamId = teamId;
                        Input.KeyId = keyId;
                        Input.PrivateKey = privateKey;
                        Input.Region = region;
                        Input.EnterpriseDomain = enterpriseDomain;
                    }
                }
                catch { }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Reload provider metadata for validation
            Provider = await ssoProviderService.GetProviderAsync(Input.ProviderType);

            // Custom validation based on provider type
            if (Input.ProviderType == ThirdPartyAuthenticationType.Apple)
            {
                if (string.IsNullOrWhiteSpace(Input.TeamId))
                    ModelState.AddModelError("Input.TeamId", "Team ID is required for Apple");
                if (string.IsNullOrWhiteSpace(Input.KeyId))
                    ModelState.AddModelError("Input.KeyId", "Key ID is required for Apple");
                if (string.IsNullOrWhiteSpace(Input.PrivateKey))
                    ModelState.AddModelError("Input.PrivateKey", "Private Key is required for Apple");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var configDto = new SSOProviderConfigDto
                {
                    ProviderType = Input.ProviderType,
                    IsEnabled = Input.IsEnabled,
                    ClientId = Input.ClientId,
                    ClientSecret = Input.ClientSecret,
                    Scopes = Input.Scopes,
                    TenantId = Input.TenantId,
                    TeamId = Input.TeamId,
                    KeyId = Input.KeyId,
                    PrivateKey = Input.PrivateKey,
                    Region = Input.Region,
                    EnterpriseDomain = Input.EnterpriseDomain
                };

                await ssoProviderService.SaveProviderConfigurationAsync(configDto);

                SuccessMessage = $"{Provider.DisplayName} configured successfully";
                return RedirectToPage("/IdentityServer/SSOProviders", new { area = "Admin" });
            }
            catch (Exception ex)
            {
                var errorDetails = ex.Message;
                if (ex.InnerException != null)
                {
                    errorDetails += $" Inner Exception: {ex.InnerException.Message}";
                }
                ErrorMessage = $"Error saving configuration: {errorDetails}";
                return Page();
            }
        }
    }
}
