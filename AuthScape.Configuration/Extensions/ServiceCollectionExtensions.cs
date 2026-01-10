using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Services.Database;
using AuthScape.Configuration.Options;
using AuthScape.Configuration.Validation;

namespace AuthScape.Configuration.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to configure AuthScape settings.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AuthScape configuration with validation and hot reload support.
    /// Binds the AppSettings section from configuration to IOptions&lt;AppSettings&gt;.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="configureOptions">Optional action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuthScapeSettings(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthScapeConfigurationOptions>? configureOptions = null)
    {
        var options = new AuthScapeConfigurationOptions();
        configureOptions?.Invoke(options);

        // Bind AppSettings from configuration
        var appSettingsSection = configuration.GetSection("AppSettings");

        if (options.ValidateOnStartup)
        {
            // Add with validation
            services.AddOptions<AppSettings>()
                .Bind(appSettingsSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Add custom validator
            services.AddSingleton<IValidateOptions<AppSettings>, AppSettingsValidator>();
        }
        else
        {
            // Add without validation
            services.Configure<AppSettings>(appSettingsSection);
        }

        // Register AppSettings directly for convenience (current value snapshot)
        services.AddSingleton(sp =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<AppSettings>>();
            return optionsMonitor.CurrentValue;
        });

        return services;
    }

    /// <summary>
    /// Adds AuthScape configuration with a custom configuration action.
    /// Useful for testing or programmatic configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureSettings">Action to configure AppSettings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuthScapeSettings(
        this IServiceCollection services,
        Action<AppSettings> configureSettings)
    {
        services.Configure(configureSettings);

        // Register AppSettings directly
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AppSettings>>();
            return options.Value;
        });

        return services;
    }

    /// <summary>
    /// Gets the AppSettings from configuration. Throws if not configured.
    /// </summary>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The AppSettings instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when AppSettings section is missing.</exception>
    public static AppSettings GetAuthScapeSettings(this IConfiguration configuration)
    {
        var settings = configuration.GetSection("AppSettings").Get<AppSettings>();
        if (settings == null)
        {
            throw new InvalidOperationException(
                "AppSettings configuration section is missing. " +
                "Ensure your configuration includes an 'AppSettings' section.");
        }
        return settings;
    }

    /// <summary>
    /// Tries to get the AppSettings from configuration.
    /// </summary>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="settings">The AppSettings instance if found.</param>
    /// <returns>True if AppSettings was found, false otherwise.</returns>
    public static bool TryGetAuthScapeSettings(this IConfiguration configuration, out AppSettings? settings)
    {
        settings = configuration.GetSection("AppSettings").Get<AppSettings>();
        return settings != null;
    }
}
