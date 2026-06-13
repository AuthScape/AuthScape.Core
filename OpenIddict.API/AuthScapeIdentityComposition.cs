using AuthScape.AuthManager;
using AuthScape.AuthManager.OpenIddict;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API
{
    /// <summary>
    /// Composition-root helper for the <b>OpenIddict example</b> host. This host validates tokens
    /// issued by the bundled <c>OpenIddict.IDP</c> authorization server, so the provider is pinned to
    /// OpenIddict rather than selected from configuration. The Keycloak example lives in the sibling
    /// <c>Keycloak.API</c> host.
    /// </summary>
    public static class AuthScapeIdentityComposition
    {
        /// <summary>
        /// Wires the AuthScape identity pipeline in OpenIddict (issuing/validating) mode, binding the
        /// "Authentication:OpenIddict" section onto the provider options.
        /// </summary>
        public static IServiceCollection AddConfiguredAuthScapeIdentity(
            this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection("Authentication");

            services.AddAuthScapeIdentity(o =>
                    o.AutoProvisionUsers = section.GetValue("AutoProvisionUsers", true))
                .UseOpenIddict(o => section.GetSection("OpenIddict").Bind(o));

            return services;
        }
    }
}
