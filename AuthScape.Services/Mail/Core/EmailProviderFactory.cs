using AuthScape.Services.Mail.Abstractions;
using AuthScape.Services.Mail.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace AuthScape.Services.Mail.Core
{
    /// <summary>
    /// Factory for creating and managing email provider instances
    /// </summary>
    public class EmailProviderFactory : IEmailProviderFactory
    {
        private readonly EmailConfiguration _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailProviderFactory> _logger;
        private readonly Dictionary<string, IEmailProvider> _providerCache;

        public EmailProviderFactory(
            IOptions<EmailConfiguration> config,
            IServiceProvider serviceProvider,
            ILogger<EmailProviderFactory> logger)
        {
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _providerCache = new Dictionary<string, IEmailProvider>(StringComparer.OrdinalIgnoreCase);
        }

        public IEmailProvider GetProvider(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            // Check cache first
            if (_providerCache.TryGetValue(providerName, out var cachedProvider))
            {
                return cachedProvider;
            }

            // Create provider based on name
            IEmailProvider provider = providerName.ToLower() switch
            {
                "sendgrid" => (IEmailProvider)_serviceProvider.GetService(typeof(Providers.SendGridEmailProvider)),
                "mailgun" => (IEmailProvider)_serviceProvider.GetService(typeof(Providers.MailgunEmailProvider)),
                "mailkit" => (IEmailProvider)_serviceProvider.GetService(typeof(Providers.MailKitEmailProvider)),
                "smtp" => (IEmailProvider)_serviceProvider.GetService(typeof(Providers.MailKitEmailProvider)),
                "smtp2go" => (IEmailProvider)_serviceProvider.GetService(typeof(Providers.Smtp2GoEmailProvider)),
                "azureemail" => (IEmailProvider)_serviceProvider.GetService(typeof(Providers.AzureEmailProvider)),
                "azure" => (IEmailProvider)_serviceProvider.GetService(typeof(Providers.AzureEmailProvider)),
                _ => throw new NotSupportedException($"Email provider '{providerName}' is not supported")
            };

            if (provider == null)
            {
                throw new InvalidOperationException($"Failed to create email provider '{providerName}'. Ensure it is registered in the dependency injection container.");
            }

            // Cache the provider
            _providerCache[providerName] = provider;

            _logger.LogInformation("Created email provider: {ProviderName}", providerName);
            return provider;
        }

        public IEmailProvider GetDefaultProvider()
        {
            if (string.IsNullOrEmpty(_config.DefaultProvider))
            {
                _logger.LogWarning("No default email provider configured. Attempting to use SendGrid as fallback.");

                // Try to find any enabled provider
                foreach (var providerConfig in _config.Providers)
                {
                    if (providerConfig.Value.Enabled)
                    {
                        _logger.LogInformation("Using {Provider} as default email provider", providerConfig.Key);
                        return GetProvider(providerConfig.Key);
                    }
                }

                throw new InvalidOperationException("No default email provider is configured and no enabled providers were found. Please configure an email provider in appsettings.json");
            }

            return GetProvider(_config.DefaultProvider);
        }
    }
}
