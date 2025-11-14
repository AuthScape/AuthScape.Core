namespace AuthScape.Services.Mail.Abstractions
{
    /// <summary>
    /// Factory for creating email provider instances
    /// </summary>
    public interface IEmailProviderFactory
    {
        /// <summary>
        /// Creates an email provider instance by name
        /// </summary>
        /// <param name="providerName">Name of the provider (e.g., "SendGrid", "Mailgun")</param>
        /// <returns>The email provider instance</returns>
        IEmailProvider GetProvider(string providerName);

        /// <summary>
        /// Gets the default configured email provider
        /// </summary>
        /// <returns>The default email provider instance</returns>
        IEmailProvider GetDefaultProvider();
    }
}
