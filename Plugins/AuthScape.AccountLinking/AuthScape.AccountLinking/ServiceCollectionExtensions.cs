using Microsoft.Extensions.DependencyInjection;

namespace AuthScape.AccountLinking;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAccountLinkingService"/>. Call this from API/IDP startup before any
    /// federation handlers (SAML, LDAP, OAuth) that depend on it.
    /// </summary>
    public static IServiceCollection AddAuthScapeAccountLinking(this IServiceCollection services)
    {
        services.AddScoped<IAccountLinkingService, AccountLinkingService>();
        return services;
    }
}
