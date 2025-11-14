using AuthScape.Services.Mail.Abstractions;
using AuthScape.Services.Mail.Core;
using AuthScape.Services.Mail.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AuthScape.Services.Mail.Configuration
{
    /// <summary>
    /// Extension methods for configuring email services in dependency injection
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the email service and all providers to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration instance</param>
        /// <param name="configSectionName">The name of the configuration section (default: "Email")</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddEmailService(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSectionName = "Email")
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            // Bind configuration
            services.Configure<EmailConfiguration>(configuration.GetSection(configSectionName));

            // Register HTTP client factory for providers that need it
            services.AddHttpClient();

            // Register all email providers
            services.AddTransient<SendGridEmailProvider>();
            services.AddTransient<MailgunEmailProvider>();
            services.AddTransient<MailKitEmailProvider>();
            services.AddTransient<Smtp2GoEmailProvider>();
            services.AddTransient<AzureEmailProvider>();

            // Register factory and service
            services.AddSingleton<IEmailProviderFactory, EmailProviderFactory>();
            services.AddTransient<IEmailService, EmailService>();

            return services;
        }

        /// <summary>
        /// Adds the email service with a custom configuration action
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureOptions">Action to configure email options</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddEmailService(
            this IServiceCollection services,
            Action<EmailConfiguration> configureOptions)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configureOptions == null)
            {
                throw new ArgumentNullException(nameof(configureOptions));
            }

            // Configure options
            services.Configure(configureOptions);

            // Register HTTP client factory for providers that need it
            services.AddHttpClient();

            // Register all email providers
            services.AddTransient<SendGridEmailProvider>();
            services.AddTransient<MailgunEmailProvider>();
            services.AddTransient<MailKitEmailProvider>();
            services.AddTransient<Smtp2GoEmailProvider>();
            services.AddTransient<AzureEmailProvider>();

            // Register factory and service
            services.AddSingleton<IEmailProviderFactory, EmailProviderFactory>();
            services.AddTransient<IEmailService, EmailService>();

            return services;
        }
    }
}
