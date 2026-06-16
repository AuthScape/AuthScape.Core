using System;
using AuthScape.AuthManager;
using AuthScape.AuthManager.Keycloak;
using AuthScape.AuthManager.OpenIddict;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API
{
    /// <summary>
    /// Composition-root helper for the single AuthScape API host. The token issuer is selected at
    /// runtime from <c>Authentication:Provider</c> ("OpenIddict" or "Keycloak") — flip that one key to
    /// switch providers, no code change or rebuild required:
    ///   - <b>OpenIddict</b>: validates tokens issued by the bundled <c>IDP</c> server.
    ///   - <b>Keycloak</b>: validates tokens against a Keycloak realm (see <c>../keycloak/</c>).
    /// Both <c>Authentication:OpenIddict</c> and <c>Authentication:Keycloak</c> sections live in
    /// appsettings.json so either provider works without editing config layout.
    /// </summary>
    public static class AuthScapeIdentityComposition
    {
        /// <summary>
        /// Wires the AuthScape identity pipeline for the provider named in <c>Authentication:Provider</c>,
        /// binding the matching <c>Authentication:OpenIddict</c> or <c>Authentication:Keycloak</c> section
        /// onto the provider options. Defaults to OpenIddict when the key is absent.
        /// </summary>
        public static IServiceCollection AddConfiguredAuthScapeIdentity(
            this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection("Authentication");
            var provider = section.GetValue<string>("Provider") ?? "OpenIddict";

            var builder = services.AddAuthScapeIdentity(o =>
                o.AutoProvisionUsers = section.GetValue("AutoProvisionUsers", true));

            if (string.Equals(provider, "Keycloak", StringComparison.OrdinalIgnoreCase))
            {
                builder.UseKeycloak(o => section.GetSection("Keycloak").Bind(o));
            }
            else
            {
                builder.UseOpenIddict(o => section.GetSection("OpenIddict").Bind(o));
            }

            return services;
        }
    }
}
