using System.Threading;
using System.Threading.Tasks;

namespace AuthScape.Services.Mail.Abstractions
{
    /// <summary>
    /// Main service interface for sending emails through the configured provider
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Sends an email using the configured email provider
        /// </summary>
        /// <param name="message">The email message to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Response indicating success or failure</returns>
        Task<IEmailResponse> SendEmailAsync(IEmailMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends an email using a specific provider (overrides configured default)
        /// </summary>
        /// <param name="message">The email message to send</param>
        /// <param name="providerName">Name of the provider to use</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Response indicating success or failure</returns>
        Task<IEmailResponse> SendEmailAsync(IEmailMessage message, string providerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the name of the currently configured default provider
        /// </summary>
        string GetDefaultProviderName();
    }
}
