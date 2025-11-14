using AuthScape.Models.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Services.Context;
using System.Linq;

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
            }
        }
    }
}
