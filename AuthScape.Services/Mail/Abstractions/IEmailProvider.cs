using System.Threading;
using System.Threading.Tasks;

namespace AuthScape.Services.Mail.Abstractions
{
    /// <summary>
    /// Represents a specific email service provider implementation (SendGrid, Mailgun, etc.)
    /// </summary>
    public interface IEmailProvider
    {
        /// <summary>
        /// Name of the email provider (e.g., "SendGrid", "Mailgun")
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Sends an email message using this provider
        /// </summary>
        /// <param name="message">The email message to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Response indicating success or failure</returns>
        Task<IEmailResponse> SendEmailAsync(IEmailMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that the provider is properly configured
        /// </summary>
        /// <returns>True if configured correctly, false otherwise</returns>
        bool IsConfigured();
    }
}
