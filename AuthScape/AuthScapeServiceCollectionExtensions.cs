using AuthScape.Analytics.Services;
using AuthScape.AuthManager;
using AuthScape.AuthManager.OpenIddict;
using AuthScape.Configuration.Extensions;
using AuthScape.Logging;
using AuthScape.Services.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Context;
using Services.Database;

namespace AuthScape;

/// <summary>
/// Single-call wiring for the AuthScape core stack: authentication (OpenIddict),
/// configuration, database, analytics, and error-tracking/logging.
/// Add optional plugins (Keycloak, LDAP, SAML, SCIM, etc.) with their own AddAuthScapeXxx() calls.
/// </summary>
public static class AuthScapeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the always-on AuthScape services in one call. Wires:
    /// <list type="bullet">
    ///   <item>AddAuthScapeSettings — binds the AppSettings configuration section.</item>
    ///   <item>AddAuthScapeDatabase — registers the DbContext (auto-detects SqlServer/Postgres/MySQL/SQLite).</item>
    ///   <item>AddAuthScapeIdentity().UseOpenIddict() — provider-agnostic identity pipeline with OpenIddict as the default token issuer.</item>
    ///   <item>IAnalyticsService — first-party analytics event/session tracking.</item>
    ///   <item>ILogService — error tracking write-path (the IDP serves the dashboard).</item>
    /// </list>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration containing an "AppSettings" section.</param>
    /// <param name="configure">Optional overrides — supply this to swap OpenIddict for another provider or tune identity options.</param>
    public static IServiceCollection AddAuthScape(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthScapeOptions>? configure = null)
    {
        var options = new AuthScapeOptions();
        configure?.Invoke(options);

        services.AddAuthScapeSettings(configuration);

        var appSettings = configuration.GetAuthScapeSettings();
        services.AddAuthScapeDatabase<DatabaseContext>(
            appSettings.DatabaseContext,
            enableSensitiveDataLogging: options.EnableSensitiveDatabaseLogging,
            useOpenIddict: true,
            lifetime: options.DatabaseLifetime);

        var identityBuilder = services.AddAuthScapeIdentity(options.ConfigureIdentity);

        // Default to OpenIddict unless the caller installed a different provider via UseKeycloak() etc.
        if (options.SkipDefaultTokenIssuer == false)
        {
            identityBuilder.UseOpenIddict(options.ConfigureOpenIddict);
        }

        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<ILogService, LogService>();
        services.AddHttpClient();

        return services;
    }
}

/// <summary>
/// Options surfaced through <see cref="AuthScapeServiceCollectionExtensions.AddAuthScape"/>.
/// </summary>
public sealed class AuthScapeOptions
{
    /// <summary>
    /// EF Core sensitive-data logging. Recommended only in Development.
    /// </summary>
    public bool EnableSensitiveDatabaseLogging { get; set; }

    /// <summary>
    /// Service lifetime for the DbContext registration. Default: Scoped.
    /// </summary>
    public ServiceLifetime DatabaseLifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Set to true to skip the default OpenIddict token issuer when wiring an alternative
    /// (for example, AuthScape.AuthManager.Keycloak).
    /// </summary>
    public bool SkipDefaultTokenIssuer { get; set; }

    /// <summary>
    /// Configure the provider-agnostic identity pipeline (auto-provisioning, mode, etc.).
    /// </summary>
    public Action<AuthScapeIdentityOptions> ConfigureIdentity { get; set; } = options =>
    {
        options.Mode = AuthProviderMode.Issuing;
        options.AutoProvisionUsers = true;
    };

    /// <summary>
    /// Configure OpenIddict server options when it is the active token issuer.
    /// </summary>
    public Action<OpenIddictProviderOptions> ConfigureOpenIddict { get; set; } = options =>
    {
        options.AccessTokenLifetime = TimeSpan.FromHours(1);
        options.AllowRefreshTokens = true;
        options.AllowClientCredentials = true;
    };
}
