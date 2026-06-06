using Microsoft.Extensions.DependencyInjection;

namespace AuthScape.Ldap;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ILdapAuthService"/>. Requires AuthScape.AccountLinking to also be registered
    /// (so the service can convert successful binds into AppUser via IAccountLinkingService).
    /// </summary>
    public static IServiceCollection AddAuthScapeLdap(this IServiceCollection services)
    {
        services.AddScoped<ILdapAuthService, LdapAuthService>();
        return services;
    }
}
