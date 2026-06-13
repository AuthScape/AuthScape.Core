using AuthScape.AuthManager;
using AuthScape.AuthManager.Keycloak;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API
{
    /// <summary>
    /// Composition-root helper for the <b>Keycloak example</b> host. This host always validates tokens
    /// against a Keycloak realm (see <c>../keycloak/docker-compose.yml</c>); it never issues its own
    /// tokens, so the provider is pinned to Keycloak rather than selected from configuration. The
    /// OpenIddict example lives in the sibling <c>OpenIddict.API</c> host.
    /// </summary>
    public static class AuthScapeIdentityComposition
    {
        /// <summary>
        /// Wires the AuthScape identity pipeline in Keycloak (validating) mode, binding the
        /// "Authentication:Keycloak" section onto the provider options.
        /// </summary>
        public static IServiceCollection AddConfiguredAuthScapeIdentity(
            this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection("Authentication");

            services.AddAuthScapeIdentity(o =>
                    o.AutoProvisionUsers = section.GetValue("AutoProvisionUsers", true))
                .UseKeycloak(o => section.GetSection("Keycloak").Bind(o));

            return services;
        }
    }
}
