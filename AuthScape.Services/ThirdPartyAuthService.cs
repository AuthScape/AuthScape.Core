using AuthScape.Models.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Services.Context;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AuthScape.Services
{
    public class ThirdPartyAuthService
    {
        public static void AddThirdPartyAutentication(IServiceCollection services)
        {
            var authBuilder = services.AddAuthentication();

            var databaseContext = services.BuildServiceProvider().GetService<DatabaseContext>();

            var thirdPartyAuths = databaseContext.ThirdPartyAuthentications
                .Where(x => x.IsEnabled)
                .ToList();

            if (!thirdPartyAuths.Any())
            {
                return;
            }

            foreach (var thirdPartyAuth in thirdPartyAuths)
            {
                var scopes = !string.IsNullOrEmpty(thirdPartyAuth.Scopes)
                    ? thirdPartyAuth.Scopes.Split(',').Select(s => s.Trim()).ToList()
                    : new System.Collections.Generic.List<string>();

                if (thirdPartyAuth.ThirdPartyAuthenticationType == ThirdPartyAuthenticationType.Facebook)
                {
                    authBuilder.AddFacebook(facebookOptions =>
                    {
                        facebookOptions.AppId = thirdPartyAuth.ClientId;
                        facebookOptions.AppSecret = thirdPartyAuth.ClientSecret;
                        foreach (var scope in scopes)
                        {
                            facebookOptions.Scope.Add(scope);
                        }
                    });
                }
                else if (thirdPartyAuth.ThirdPartyAuthenticationType == ThirdPartyAuthenticationType.Google)
                {
                    authBuilder.AddGoogle(googleOptions =>
                    {
                        googleOptions.ClientId = thirdPartyAuth.ClientId;
                        googleOptions.ClientSecret = thirdPartyAuth.ClientSecret;
                        foreach (var scope in scopes)
                        {
                            googleOptions.Scope.Add(scope);
                        }
                    });
                }
                else if (thirdPartyAuth.ThirdPartyAuthenticationType == ThirdPartyAuthenticationType.Microsoft)
                {
                    authBuilder.AddMicrosoftAccount(microsoftOptions =>
                    {
                        microsoftOptions.ClientId = thirdPartyAuth.ClientId;
                        microsoftOptions.ClientSecret = thirdPartyAuth.ClientSecret;
                        foreach (var scope in scopes)
                        {
                            microsoftOptions.Scope.Add(scope);
                        }
                    });
                }
                else if (thirdPartyAuth.ThirdPartyAuthenticationType == ThirdPartyAuthenticationType.Github)
                {
                    authBuilder.AddGitHub(options =>
                     {
                         options.ClientId = thirdPartyAuth.ClientId;
                         options.ClientSecret = thirdPartyAuth.ClientSecret;
                         foreach (var scope in scopes)
                         {
                             options.Scope.Add(scope);
                         }
                     });
                }
                else if (thirdPartyAuth.ThirdPartyAuthenticationType == ThirdPartyAuthenticationType.Keycloak)
                {
                    var authority = ExtractAdditionalSetting(thirdPartyAuth.AdditionalSettings, "Authority");
                    if (string.IsNullOrEmpty(authority))
                    {
                        // Authority is mandatory for Keycloak federation; skip silently to avoid breaking startup
                        // when an admin has enabled Keycloak before fully configuring the realm URL.
                        continue;
                    }

                    var schemeName = ThirdPartyAuthenticationType.Keycloak.ToString();
                    authBuilder.AddOpenIdConnect(schemeName, "Keycloak", oidcOptions =>
                    {
                        oidcOptions.Authority = authority;
                        oidcOptions.ClientId = thirdPartyAuth.ClientId;
                        oidcOptions.ClientSecret = thirdPartyAuth.ClientSecret;
                        oidcOptions.ResponseType = OpenIdConnectResponseType.Code;
                        oidcOptions.SaveTokens = true;
                        oidcOptions.GetClaimsFromUserInfoEndpoint = true;
                        oidcOptions.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;

                        oidcOptions.Scope.Clear();
                        var oidcScopes = scopes.Count > 0 ? scopes : new List<string> { "openid", "profile", "email" };
                        foreach (var scope in oidcScopes)
                        {
                            oidcOptions.Scope.Add(scope);
                        }

                        oidcOptions.CallbackPath = !string.IsNullOrEmpty(thirdPartyAuth.RedirectUri)
                            ? thirdPartyAuth.RedirectUri
                            : "/signin-keycloak";
                    });
                }
            }
        }

        private static string ExtractAdditionalSetting(string additionalSettingsJson, string key)
        {
            if (string.IsNullOrWhiteSpace(additionalSettingsJson))
                return null;

            try
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(additionalSettingsJson);
                if (settings != null && settings.TryGetValue(key, out var value))
                    return value;
            }
            catch (JsonException)
            {
                // Malformed JSON in DB — treat as no setting and let caller handle.
            }
            return null;
        }
    }
}
