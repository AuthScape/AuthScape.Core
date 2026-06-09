using Microsoft.Extensions.DependencyInjection;

namespace AuthScape.Saml2;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISamlService"/> and the metadata refresh background service.
    /// Requires AuthScape.AccountLinking to also be registered.
    /// </summary>
    public static IServiceCollection AddAuthScapeSaml2(this IServiceCollection services)
    {
        services.AddScoped<ISamlService, SamlService>();
        services.AddHttpClient(); // SamlMetadataRefreshService requires IHttpClientFactory; AddHttpClient is idempotent
        services.AddHostedService<SamlMetadataRefreshService>();
        return services;
    }
}
