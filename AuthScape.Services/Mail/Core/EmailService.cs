using AuthScape.Services.Mail.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuthScape.Services.Mail.Core
{
    /// <summary>
    /// Main email service implementation
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IEmailProviderFactory _providerFactory;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IEmailProviderFactory providerFactory, ILogger<EmailService> logger)
        {
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEmailResponse> SendEmailAsync(IEmailMessage message, CancellationToken cancellationToken = default)
        {
            try
            {
                var provider = _providerFactory.GetDefaultProvider();
                _logger.LogInformation("Sending email using default provider: {Provider}", provider.ProviderName);

                return await provider.SendEmailAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email using default provider");
                return EmailResponse.Failure("Unknown", ex.Message);
            }
        }

        public async Task<IEmailResponse> SendEmailAsync(IEmailMessage message, string providerName, CancellationToken cancellationToken = default)
        {
            try
            {
                var provider = _providerFactory.GetProvider(providerName);
                _logger.LogInformation("Sending email using specified provider: {Provider}", providerName);

                return await provider.SendEmailAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email using provider: {Provider}", providerName);
                return EmailResponse.Failure(providerName, ex.Message);
            }
        }

        public string GetDefaultProviderName()
        {
            var provider = _providerFactory.GetDefaultProvider();
            return provider?.ProviderName ?? "None";
        }
    }
}
