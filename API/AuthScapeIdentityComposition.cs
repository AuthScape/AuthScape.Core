using AuthScape.AuthManager;
using AuthScape.AuthManager.Keycloak;
using AuthScape.AuthManager.OpenIddict;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace API
{
    /// <summary>
    /// Composition-root helper that wires the AuthScape identity pipeline from configuration instead
    /// of hardcoding the provider in <see cref="Startup"/>. Lives in the host project because this is
    /// the only place that references both the OpenIddict and Keycloak adapter assemblies.
    /// </summary>
    public static class AuthScapeIdentityComposition
    {
        /// <summary>
        /// Reads the "Authentication" section, selects the provider from "Provider", and binds the
        /// matching sub-section onto that provider's options. Switching providers is a config change
        /// (appsettings / env var <c>Authentication__Provider</c>) — no recompile.
        /// </summary>
        public static IServiceCollection AddConfiguredAuthScapeIdentity(
            this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection("Authentication");
            var provider = section["Provider"] ?? "OpenIddict";

            // Mode is force-set by each Use* provider (OpenIddict→Issuing, Keycloak→Validating),
            // so it is intentionally not set here.
            var builder = services.AddAuthScapeIdentity(o =>
                o.AutoProvisionUsers = section.GetValue("AutoProvisionUsers", true));

            switch (provider.Trim().ToLowerInvariant())
            {
                case "openiddict":
                    builder.UseOpenIddict(o => section.GetSection("OpenIddict").Bind(o));
                    break;

                case "keycloak":
                    builder.UseKeycloak(o => section.GetSection("Keycloak").Bind(o));
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Authentication:Provider '{provider}' is not supported. "
                        + "Use 'OpenIddict' or 'Keycloak'.");
            }

            return services;
        }
    }
}
