using AuthScape.Services.Mail.Abstractions;
using AuthScape.Services.Mail.Configuration;
using AuthScape.Services.Mail.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuthScape.Services.Mail.Providers
{
    /// <summary>
    /// SendGrid email provider implementation
    /// </summary>
    public class SendGridEmailProvider : IEmailProvider
    {
        private readonly SendGridConfig _config;
        private readonly ILogger<SendGridEmailProvider> _logger;
        private readonly SendGridClient _client;

        public string ProviderName => "SendGrid";

        public SendGridEmailProvider(IOptions<EmailConfiguration> emailConfig, ILogger<SendGridEmailProvider> logger)
        {
            var config = emailConfig.Value;
            if (config.Providers.TryGetValue("SendGrid", out var providerConfig) && providerConfig is SendGridConfig sendGridConfig)
            {
                _config = sendGridConfig;
            }
            else if (config.Providers.TryGetValue("SendGrid", out var baseConfig))
            {
                // Convert base config to SendGridConfig
                _config = new SendGridConfig
                {
                    Enabled = baseConfig.Enabled,
                    ApiKey = baseConfig.ApiKey,
                    Settings = baseConfig.Settings
                };
            }
            else
            {
                _config = new SendGridConfig();
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                _client = new SendGridClient(_config.ApiKey);
            }
        }

        public bool IsConfigured()
        {
            return _config?.Enabled == true && !string.IsNullOrEmpty(_config?.ApiKey);
        }

        public async Task<IEmailResponse> SendEmailAsync(IEmailMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured())
            {
                _logger.LogError("SendGrid provider is not configured properly");
                return EmailResponse.Failure(ProviderName, "SendGrid is not configured. Please provide an API key.");
            }

            try
            {
                var from = new EmailAddress(message.FromEmail, message.FromName);
                var msg = new SendGridMessage();

                msg.SetFrom(from);
                msg.Subject = message.Subject;
                msg.PlainTextContent = message.TextContent;
                msg.HtmlContent = message.HtmlContent;

                // Add recipients
                if (message.To != null && message.To.Any())
                {
                    foreach (var recipient in message.To)
                    {
                        msg.AddTo(new EmailAddress(recipient.Email, recipient.Name));
                    }
                }

                // Add CC recipients
                if (message.Cc != null && message.Cc.Any())
                {
                    foreach (var cc in message.Cc)
                    {
                        msg.AddCc(new EmailAddress(cc.Email, cc.Name));
                    }
                }

                // Add BCC recipients
                if (message.Bcc != null && message.Bcc.Any())
                {
                    foreach (var bcc in message.Bcc)
                    {
                        msg.AddBcc(new EmailAddress(bcc.Email, bcc.Name));
                    }
                }

                // Add reply-to
                if (!string.IsNullOrEmpty(message.ReplyToEmail))
                {
                    msg.SetReplyTo(new EmailAddress(message.ReplyToEmail, message.ReplyToName));
                }

                // Add attachments
                if (message.Attachments != null && message.Attachments.Any())
                {
                    foreach (var attachment in message.Attachments)
                    {
                        var base64Content = Convert.ToBase64String(attachment.Content);
                        msg.AddAttachment(attachment.FileName, base64Content, attachment.ContentType, "attachment", attachment.ContentId);
                    }
                }

                // Add custom headers
                if (message.Headers != null && message.Headers.Any())
                {
                    foreach (var header in message.Headers)
                    {
                        msg.AddHeader(header.Key, header.Value);
                    }
                }

                // Add categories (tags)
                if (message.Tags != null && message.Tags.Any())
                {
                    msg.AddCategories(message.Tags);
                }

                var response = await _client.SendEmailAsync(msg, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var messageId = response.Headers.GetValues("X-Message-Id")?.FirstOrDefault();
                    _logger.LogInformation("Email sent successfully via SendGrid. MessageId: {MessageId}", messageId);

                    return EmailResponse.Success(ProviderName, messageId, (int)response.StatusCode);
                }
                else
                {
                    var errorBody = await response.Body.ReadAsStringAsync();
                    _logger.LogError("Failed to send email via SendGrid. StatusCode: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorBody);

                    return EmailResponse.Failure(ProviderName, errorBody, (int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending email via SendGrid");
                return EmailResponse.Failure(ProviderName, ex.Message);
            }
        }
    }
}
