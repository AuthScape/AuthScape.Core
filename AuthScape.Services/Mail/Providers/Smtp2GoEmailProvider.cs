using AuthScape.Services.Mail.Abstractions;
using AuthScape.Services.Mail.Configuration;
using AuthScape.Services.Mail.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuthScape.Services.Mail.Providers
{
    /// <summary>
    /// SMTP2GO email provider implementation (uses SMTP)
    /// Can use MailKit for SMTP connectivity
    /// </summary>
    public class Smtp2GoEmailProvider : IEmailProvider
    {
        private readonly SmtpConfig _config;
        private readonly ILogger<Smtp2GoEmailProvider> _logger;
        private readonly MailKitEmailProvider _mailKitProvider;

        public string ProviderName => "SMTP2GO";

        public Smtp2GoEmailProvider(IOptions<EmailConfiguration> emailConfig, ILogger<Smtp2GoEmailProvider> logger, ILogger<MailKitEmailProvider> mailKitLogger)
        {
            var config = emailConfig.Value;
            if (config.Providers.TryGetValue("SMTP2GO", out var providerConfig) && providerConfig is SmtpConfig smtpConfig)
            {
                _config = smtpConfig;
            }
            else if (config.Providers.TryGetValue("SMTP2GO", out var baseConfig))
            {
                _config = new SmtpConfig
                {
                    Enabled = baseConfig.Enabled,
                    ApiKey = baseConfig.ApiKey,
                    Host = baseConfig.Settings.GetValueOrDefault("Host", "mail.smtp2go.com"),
                    Port = int.Parse(baseConfig.Settings.GetValueOrDefault("Port", "2525")),
                    Username = baseConfig.Settings.GetValueOrDefault("Username", ""),
                    Password = baseConfig.Settings.GetValueOrDefault("Password", ""),
                    UseSsl = bool.Parse(baseConfig.Settings.GetValueOrDefault("UseSsl", "true")),
                    Settings = baseConfig.Settings
                };
            }
            else
            {
                _config = new SmtpConfig();
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // SMTP2GO uses SMTP protocol, so we can leverage MailKit
            var smtp2GoEmailConfig = new EmailConfiguration
            {
                Providers = new System.Collections.Generic.Dictionary<string, EmailProviderConfig>
                {
                    { "MailKit", _config }
                }
            };
            _mailKitProvider = new MailKitEmailProvider(Options.Create(smtp2GoEmailConfig), mailKitLogger);
        }

        public bool IsConfigured()
        {
            return _config?.Enabled == true &&
                   !string.IsNullOrEmpty(_config?.Host) &&
                   !string.IsNullOrEmpty(_config?.Username) &&
                   !string.IsNullOrEmpty(_config?.Password);
        }

        public async Task<IEmailResponse> SendEmailAsync(IEmailMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured())
            {
                _logger.LogError("SMTP2GO provider is not configured properly");
                return EmailResponse.Failure(ProviderName, "SMTP2GO is not configured. Please provide host, username, and password.");
            }

            try
            {
                _logger.LogInformation("Sending email via SMTP2GO using SMTP protocol");
                var response = await _mailKitProvider.SendEmailAsync(message, cancellationToken);

                // Update provider name to SMTP2GO
                response.Provider = ProviderName;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending email via SMTP2GO");
                return EmailResponse.Failure(ProviderName, ex.Message);
            }
        }
    }
}
