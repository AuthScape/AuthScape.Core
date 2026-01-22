using AuthScape.Services.Database;
using Microsoft.Extensions.DependencyInjection;
using Services.Context;

namespace Services.Database;

/// <summary>
/// Extension methods for registering DatabaseContext with dependency injection.
/// </summary>
public static class DatabaseContextExtensions
{
    /// <summary>
    /// Adds the DatabaseContext with auto-detected provider based on the connection string in AppSettings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="appSettings">The application settings containing DatabaseContext connection string.</param>
    /// <param name="enableSensitiveDataLogging">Enable sensitive data logging (for development).</param>
    /// <param name="useOpenIddict">Whether to configure OpenIddict entity sets.</param>
    /// <param name="lifetime">The service lifetime. Defaults to Scoped.</param>
    public static IServiceCollection AddAuthScapeDatabase(
        this IServiceCollection services,
        AppSettings appSettings,
        bool enableSensitiveDataLogging = false,
        bool useOpenIddict = true,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        return services.AddAuthScapeDatabase<DatabaseContext>(
            appSettings.DatabaseContext,
            enableSensitiveDataLogging,
            useOpenIddict,
            lifetime);
    }
}
