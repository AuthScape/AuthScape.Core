using AuthScape.Services.Mail.Abstractions;
using AuthScape.Services.Mail.Configuration;
using AuthScape.Services.Mail.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuthScape.Services.Mail.Providers
{
    /// <summary>
    /// MailKit (SMTP) email provider implementation
    /// </summary>
    public class MailKitEmailProvider : IEmailProvider
    {
        private readonly SmtpConfig _config;
        private readonly ILogger<MailKitEmailProvider> _logger;

        public string ProviderName => "MailKit";

        public MailKitEmailProvider(IOptions<EmailConfiguration> emailConfig, ILogger<MailKitEmailProvider> logger)
        {
            var config = emailConfig.Value;
            if (config.Providers.TryGetValue("MailKit", out var providerConfig) && providerConfig is SmtpConfig smtpConfig)
            {
                _config = smtpConfig;
            }
            else if (config.Providers.TryGetValue("MailKit", out var baseConfig))
            {
                _config = new SmtpConfig
                {
                    Enabled = baseConfig.Enabled,
                    ApiKey = baseConfig.ApiKey,
                    Settings = baseConfig.Settings,
                    Host = baseConfig.Settings.GetValueOrDefault("Host", ""),
                    Port = int.Parse(baseConfig.Settings.GetValueOrDefault("Port", "587")),
                    Username = baseConfig.Settings.GetValueOrDefault("Username", ""),
                    Password = baseConfig.Settings.GetValueOrDefault("Password", ""),
                    UseSsl = bool.Parse(baseConfig.Settings.GetValueOrDefault("UseSsl", "true"))
                };
            }
            else
            {
                _config = new SmtpConfig();
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                _logger.LogError("MailKit (SMTP) provider is not configured properly");
                return EmailResponse.Failure(ProviderName, "MailKit (SMTP) is not configured. Please provide host, username, and password.");
            }

            try
            {
                var mimeMessage = new MimeMessage();

                // From
                mimeMessage.From.Add(new MailboxAddress(message.FromName, message.FromEmail));

                // To
                if (message.To != null && message.To.Any())
                {
                    foreach (var to in message.To)
                    {
                        mimeMessage.To.Add(new MailboxAddress(to.Name, to.Email));
                    }
                }

                // Cc
                if (message.Cc != null && message.Cc.Any())
                {
                    foreach (var cc in message.Cc)
                    {
                        mimeMessage.Cc.Add(new MailboxAddress(cc.Name, cc.Email));
                    }
                }

                // Bcc
                if (message.Bcc != null && message.Bcc.Any())
                {
                    foreach (var bcc in message.Bcc)
                    {
                        mimeMessage.Bcc.Add(new MailboxAddress(bcc.Name, bcc.Email));
                    }
                }

                // Reply-To
                if (!string.IsNullOrEmpty(message.ReplyToEmail))
                {
                    mimeMessage.ReplyTo.Add(new MailboxAddress(message.ReplyToName, message.ReplyToEmail));
                }

                // Subject
                mimeMessage.Subject = message.Subject;

                // Body
                var builder = new BodyBuilder();
                if (!string.IsNullOrEmpty(message.TextContent))
                {
                    builder.TextBody = message.TextContent;
                }
                if (!string.IsNullOrEmpty(message.HtmlContent))
                {
                    builder.HtmlBody = message.HtmlContent;
                }

                // Attachments
                if (message.Attachments != null && message.Attachments.Any())
                {
                    foreach (var attachment in message.Attachments)
                    {
                        builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
                    }
                }

                mimeMessage.Body = builder.ToMessageBody();

                // Custom headers
                if (message.Headers != null && message.Headers.Any())
                {
                    foreach (var header in message.Headers)
                    {
                        mimeMessage.Headers.Add(header.Key, header.Value);
                    }
                }

                using (var client = new SmtpClient())
                {
                    var secureSocketOptions = _config.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
                    await client.ConnectAsync(_config.Host, _config.Port, secureSocketOptions, cancellationToken);
                    await client.AuthenticateAsync(_config.Username, _config.Password, cancellationToken);

                    var result = await client.SendAsync(mimeMessage, cancellationToken);
                    await client.DisconnectAsync(true, cancellationToken);

                    _logger.LogInformation("Email sent successfully via MailKit (SMTP)");
                    return EmailResponse.Success(ProviderName, mimeMessage.MessageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending email via MailKit (SMTP)");
                return EmailResponse.Failure(ProviderName, ex.Message);
            }
        }
    }
}
