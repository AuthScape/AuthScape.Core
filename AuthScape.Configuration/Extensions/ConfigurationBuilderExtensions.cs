using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using AuthScape.Configuration.Options;
using AuthScape.Configuration.Providers;
using AuthScape.Models;

namespace AuthScape.Configuration.Extensions;

/// <summary>
/// Extension methods for IConfigurationBuilder to add AuthScape configuration sources.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds AuthScape configuration sources based on the specified source priority.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="source">Which configuration source to prioritize.</param>
    /// <param name="configureOptions">Optional action to configure options.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddAuthScapeConfiguration(
        this IConfigurationBuilder builder,
        IHostEnvironment environment,
        ConfigurationSource source = ConfigurationSource.Default,
        Action<AuthScapeConfigurationOptions>? configureOptions = null)
    {
        var options = new AuthScapeConfigurationOptions();
        configureOptions?.Invoke(options);

        switch (source)
        {
            case ConfigurationSource.ProjectOnly:
                // Only use project appsettings.json
                builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: options.EnableHotReload);
                builder.AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: options.EnableHotReload);
                break;

            case ConfigurationSource.SharedOnly:
                // Only use shared authscape.json
                AddSharedConfiguration(builder, environment, options);
                break;

            case ConfigurationSource.EnvironmentOnly:
                // Only use environment variables and secrets
                builder.AddEnvironmentVariables();
                if (!string.IsNullOrEmpty(options.EnvironmentVariablePrefix))
                {
                    builder.AddEnvironmentVariables(options.EnvironmentVariablePrefix);
                }
                AddSecrets(builder, environment, options);
                break;

            case ConfigurationSource.SharedSettings:
                // Prioritize shared over project: appsettings → shared → env → secrets
                builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: options.EnableHotReload);
                builder.AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: options.EnableHotReload);
                AddSharedConfiguration(builder, environment, options);
                builder.AddEnvironmentVariables();
                if (!string.IsNullOrEmpty(options.EnvironmentVariablePrefix))
                {
                    builder.AddEnvironmentVariables(options.EnvironmentVariablePrefix);
                }
                AddSecrets(builder, environment, options);
                break;

            case ConfigurationSource.EnvironmentVariables:
                // Prioritize environment: JSON → env (highest) → secrets
                AddSharedConfiguration(builder, environment, options);
                builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: options.EnableHotReload);
                builder.AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: options.EnableHotReload);
                builder.AddEnvironmentVariables();
                if (!string.IsNullOrEmpty(options.EnvironmentVariablePrefix))
                {
                    builder.AddEnvironmentVariables(options.EnvironmentVariablePrefix);
                }
                AddSecrets(builder, environment, options);
                break;

            case ConfigurationSource.ProjectSettings:
            case ConfigurationSource.Default:
            default:
                // Default: shared → project → user secrets → env → key vault/aws
                AddSharedConfiguration(builder, environment, options);
                builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: options.EnableHotReload);
                builder.AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: options.EnableHotReload);

                // User Secrets (development only)
                if (environment.IsDevelopment())
                {
                    try
                    {
                        var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
                        if (entryAssembly != null)
                        {
                            builder.AddUserSecrets(entryAssembly, optional: true);
                        }
                    }
                    catch
                    {
                        // Ignore if user secrets not configured
                    }
                }

                builder.AddEnvironmentVariables();
                if (!string.IsNullOrEmpty(options.EnvironmentVariablePrefix))
                {
                    builder.AddEnvironmentVariables(options.EnvironmentVariablePrefix);
                }
                AddSecrets(builder, environment, options);
                break;
        }

        return builder;
    }

    private static void AddSharedConfiguration(IConfigurationBuilder builder, IHostEnvironment environment, AuthScapeConfigurationOptions options)
    {
        if (options.UseSharedConfiguration)
        {
            var sharedPath = options.SharedConfigurationPath
                ?? FindSharedConfigurationPath(environment.ContentRootPath);

            if (!string.IsNullOrEmpty(sharedPath))
            {
                var baseConfig = Path.Combine(sharedPath, "authscape.json");
                var envConfig = Path.Combine(sharedPath, $"authscape.{environment.EnvironmentName}.json");

                builder.AddJsonFile(baseConfig, optional: true, reloadOnChange: options.EnableHotReload);
                builder.AddJsonFile(envConfig, optional: true, reloadOnChange: options.EnableHotReload);
            }
        }
    }

    private static void AddSecrets(IConfigurationBuilder builder, IHostEnvironment environment, AuthScapeConfigurationOptions options)
    {
        // Azure Key Vault
        if (options.AzureKeyVault?.Enabled == true && !string.IsNullOrEmpty(options.AzureKeyVault.VaultUri))
        {
            AddAzureKeyVault(builder, options.AzureKeyVault);
        }

        // AWS Secrets Manager
        if (options.AwsSecretsManager?.Enabled == true && !string.IsNullOrEmpty(options.AwsSecretsManager.SecretId))
        {
            builder.Add(new AwsSecretsManagerConfigurationSource(options.AwsSecretsManager));
        }
    }

    /// <summary>
    /// Adds AuthScape configuration using options from a pre-built configuration.
    /// Useful when options are stored in a bootstrap configuration file.
    /// </summary>
    public static IConfigurationBuilder AddAuthScapeConfiguration(
        this IConfigurationBuilder builder,
        IHostEnvironment environment,
        IConfiguration bootstrapConfiguration)
    {
        var options = bootstrapConfiguration
            .GetSection("AuthScapeConfiguration")
            .Get<AuthScapeConfigurationOptions>() ?? new AuthScapeConfigurationOptions();

        return builder.AddAuthScapeConfiguration(environment, ConfigurationSource.Default, opt =>
        {
            opt.UseSharedConfiguration = options.UseSharedConfiguration;
            opt.SharedConfigurationPath = options.SharedConfigurationPath;
            opt.ValidateOnStartup = options.ValidateOnStartup;
            opt.EnableHotReload = options.EnableHotReload;
            opt.EnvironmentVariablePrefix = options.EnvironmentVariablePrefix;
            opt.AzureKeyVault = options.AzureKeyVault;
            opt.AwsSecretsManager = options.AwsSecretsManager;
        });
    }

    private static void AddAzureKeyVault(IConfigurationBuilder builder, AzureKeyVaultOptions options)
    {
        try
        {
            var vaultUri = new Uri(options.VaultUri!);

            if (options.UseManagedIdentity)
            {
                // Use DefaultAzureCredential which tries multiple auth methods:
                // Managed Identity, Visual Studio, Azure CLI, etc.
                var credential = new DefaultAzureCredential();
                builder.AddAzureKeyVault(vaultUri, credential,
                    new Azure.Extensions.AspNetCore.Configuration.Secrets.AzureKeyVaultConfigurationOptions
                    {
                        ReloadInterval = options.ReloadInterval
                    });
            }
            else if (!string.IsNullOrEmpty(options.TenantId) &&
                     !string.IsNullOrEmpty(options.ClientId) &&
                     !string.IsNullOrEmpty(options.ClientSecret))
            {
                // Use client credentials
                var credential = new ClientSecretCredential(
                    options.TenantId,
                    options.ClientId,
                    options.ClientSecret);

                builder.AddAzureKeyVault(vaultUri, credential,
                    new Azure.Extensions.AspNetCore.Configuration.Secrets.AzureKeyVaultConfigurationOptions
                    {
                        ReloadInterval = options.ReloadInterval
                    });
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - allow application to start and use other config sources
            Console.WriteLine($"Warning: Failed to configure Azure Key Vault: {ex.Message}");
        }
    }

    /// <summary>
    /// Walks up the directory tree to find the shared configuration folder.
    /// Looks for a 'Configuration' folder containing 'authscape.json' or
    /// 'authscape.json' directly in the solution root.
    /// </summary>
    private static string? FindSharedConfigurationPath(string startPath)
    {
        var directory = new DirectoryInfo(startPath);

        while (directory != null)
        {
            // Check for Configuration subfolder
            var configFolder = Path.Combine(directory.FullName, "Configuration");
            if (Directory.Exists(configFolder))
            {
                var configFile = Path.Combine(configFolder, "authscape.json");
                if (File.Exists(configFile))
                {
                    return configFolder;
                }
            }

            // Check for authscape.json directly in this folder (solution root)
            var directConfig = Path.Combine(directory.FullName, "authscape.json");
            if (File.Exists(directConfig))
            {
                return directory.FullName;
            }

            // Check if we've reached a solution file (good stopping point)
            if (directory.GetFiles("*.sln").Length > 0)
            {
                // Check Configuration folder at solution level one more time
                var slnConfigFolder = Path.Combine(directory.FullName, "Configuration");
                if (Directory.Exists(slnConfigFolder))
                {
                    return slnConfigFolder;
                }
                break;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
