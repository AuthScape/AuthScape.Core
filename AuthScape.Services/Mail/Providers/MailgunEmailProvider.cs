using AuthScape.Services.Mail.Abstractions;
using AuthScape.Services.Mail.Configuration;
using AuthScape.Services.Mail.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AuthScape.Services.Mail.Providers
{
    /// <summary>
    /// Mailgun email provider implementation
    /// </summary>
    public class MailgunEmailProvider : IEmailProvider
    {
        private readonly MailgunConfig _config;
        private readonly ILogger<MailgunEmailProvider> _logger;
        private readonly HttpClient _httpClient;

        public string ProviderName => "Mailgun";

        public MailgunEmailProvider(IOptions<EmailConfiguration> emailConfig, ILogger<MailgunEmailProvider> logger, IHttpClientFactory httpClientFactory)
        {
            var config = emailConfig.Value;
            if (config.Providers.TryGetValue("Mailgun", out var providerConfig) && providerConfig is MailgunConfig mailgunConfig)
            {
                _config = mailgunConfig;
            }
            else if (config.Providers.TryGetValue("Mailgun", out var baseConfig))
            {
                _config = new MailgunConfig
                {
                    Enabled = baseConfig.Enabled,
                    ApiKey = baseConfig.ApiKey,
                    Settings = baseConfig.Settings,
                    Domain = baseConfig.Settings.GetValueOrDefault("Domain", ""),
                    BaseUrl = baseConfig.Settings.GetValueOrDefault("BaseUrl", "https://api.mailgun.net/v3")
                };
            }
            else
            {
                _config = new MailgunConfig();
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
        }

        public bool IsConfigured()
        {
            return _config?.Enabled == true &&
                   !string.IsNullOrEmpty(_config?.ApiKey) &&
                   !string.IsNullOrEmpty(_config?.Domain);
        }

        public async Task<IEmailResponse> SendEmailAsync(IEmailMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured())
            {
                _logger.LogError("Mailgun provider is not configured properly");
                return EmailResponse.Failure(ProviderName, "Mailgun is not configured. Please provide an API key and domain.");
            }

            try
            {
                var formContent = new List<KeyValuePair<string, string>>();

                // From
                formContent.Add(new KeyValuePair<string, string>("from", $"{message.FromName} <{message.FromEmail}>"));

                // To
                if (message.To != null && message.To.Any())
                {
                    foreach (var to in message.To)
                    {
                        formContent.Add(new KeyValuePair<string, string>("to", string.IsNullOrEmpty(to.Name) ? to.Email : $"{to.Name} <{to.Email}>"));
                    }
                }

                // Cc
                if (message.Cc != null && message.Cc.Any())
                {
                    foreach (var cc in message.Cc)
                    {
                        formContent.Add(new KeyValuePair<string, string>("cc", string.IsNullOrEmpty(cc.Name) ? cc.Email : $"{cc.Name} <{cc.Email}>"));
                    }
                }

                // Bcc
                if (message.Bcc != null && message.Bcc.Any())
                {
                    foreach (var bcc in message.Bcc)
                    {
                        formContent.Add(new KeyValuePair<string, string>("bcc", string.IsNullOrEmpty(bcc.Name) ? bcc.Email : $"{bcc.Name} <{bcc.Email}>"));
                    }
                }

                // Subject
                formContent.Add(new KeyValuePair<string, string>("subject", message.Subject));

                // Text content
                if (!string.IsNullOrEmpty(message.TextContent))
                {
                    formContent.Add(new KeyValuePair<string, string>("text", message.TextContent));
                }

                // HTML content
                if (!string.IsNullOrEmpty(message.HtmlContent))
                {
                    formContent.Add(new KeyValuePair<string, string>("html", message.HtmlContent));
                }

                // Reply-To
                if (!string.IsNullOrEmpty(message.ReplyToEmail))
                {
                    formContent.Add(new KeyValuePair<string, string>("h:Reply-To", string.IsNullOrEmpty(message.ReplyToName) ? message.ReplyToEmail : $"{message.ReplyToName} <{message.ReplyToEmail}>"));
                }

                // Custom headers
                if (message.Headers != null && message.Headers.Any())
                {
                    foreach (var header in message.Headers)
                    {
                        formContent.Add(new KeyValuePair<string, string>($"h:{header.Key}", header.Value));
                    }
                }

                // Tags
                if (message.Tags != null && message.Tags.Any())
                {
                    foreach (var tag in message.Tags)
                    {
                        formContent.Add(new KeyValuePair<string, string>("o:tag", tag));
                    }
                }

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/{_config.Domain}/messages");
                request.Content = new FormUrlEncodedContent(formContent);

                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{_config.ApiKey}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Email sent successfully via Mailgun");
                    return EmailResponse.Success(ProviderName, null, (int)response.StatusCode);
                }
                else
                {
                    _logger.LogError("Failed to send email via Mailgun. StatusCode: {StatusCode}, Error: {Error}",
                        response.StatusCode, responseBody);
                    return EmailResponse.Failure(ProviderName, responseBody, (int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending email via Mailgun");
                return EmailResponse.Failure(ProviderName, ex.Message);
            }
        }
    }
}
