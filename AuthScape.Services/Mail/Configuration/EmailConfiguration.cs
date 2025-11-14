using System.Collections.Generic;

namespace AuthScape.Services.Mail.Configuration
{
    /// <summary>
    /// Main email configuration
    /// </summary>
    public class EmailConfiguration
    {
        /// <summary>
        /// Default email provider to use (e.g., "SendGrid", "Mailgun")
        /// </summary>
        public string DefaultProvider { get; set; }

        /// <summary>
        /// Default sender email address
        /// </summary>
        public string DefaultFromEmail { get; set; }

        /// <summary>
        /// Default sender display name
        /// </summary>
        public string DefaultFromName { get; set; }

        /// <summary>
        /// Provider-specific configurations
        /// </summary>
        public Dictionary<string, EmailProviderConfig> Providers { get; set; }

        public EmailConfiguration()
        {
            Providers = new Dictionary<string, EmailProviderConfig>();
        }
    }

    /// <summary>
    /// Base configuration for an email provider
    /// </summary>
    public class EmailProviderConfig
    {
        /// <summary>
        /// Whether this provider is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// API key or primary credential
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Additional configuration properties specific to each provider
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }

        public EmailProviderConfig()
        {
            Settings = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// SendGrid specific configuration
    /// </summary>
    public class SendGridConfig : EmailProviderConfig
    {
        public string TemplateId { get; set; }
    }

    /// <summary>
    /// Mailgun specific configuration
    /// </summary>
    public class MailgunConfig : EmailProviderConfig
    {
        public string Domain { get; set; }
        public string BaseUrl { get; set; }
    }

    /// <summary>
    /// SMTP configuration (for MailKit and SMTP2GO)
    /// </summary>
    public class SmtpConfig : EmailProviderConfig
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseSsl { get; set; }
    }

    /// <summary>
    /// Azure Communication Email configuration
    /// </summary>
    public class AzureEmailConfig : EmailProviderConfig
    {
        public string ConnectionString { get; set; }
    }
}
