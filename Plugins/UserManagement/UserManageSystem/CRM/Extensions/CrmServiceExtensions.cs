using AuthScape.UserManageSystem.CRM.Interfaces;
using AuthScape.UserManageSystem.CRM.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AuthScape.UserManageSystem.CRM.Extensions;

/// <summary>
/// Extension methods for registering CRM services in the DI container
/// </summary>
public static class CrmServiceExtensions
{
    /// <summary>
    /// Adds AuthScape CRM services to the service collection
    /// </summary>
    public static IServiceCollection AddAuthScapeCrm(this IServiceCollection services)
    {
        // Register the provider factory
        services.AddScoped<ICrmProviderFactory, CrmProviderFactory>();

        // Register the sync progress service (singleton for tracking active syncs across requests)
        services.AddSingleton<ICrmSyncProgressService, CrmSyncProgressService>();

        // Register the sync service
        services.AddScoped<ICrmSyncService, CrmSyncService>();

        // Register the entity mapper
        services.AddScoped<ICrmEntityMapper, CrmEntityMapperService>();

        // Register HttpClientFactory if not already registered
        services.AddHttpClient();

        return services;
    }
}
