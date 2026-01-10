using AuthScape.Models;
using AuthScape.Models.PaymentGateway.Stripe;
using Models.AppSettings;
using System.ComponentModel.DataAnnotations;

namespace Services.Database
{
    /// <summary>
    /// AuthScape application settings. Configure in appsettings.json or shared authscape.json.
    /// Supports multiple configuration sources: JSON files, User Secrets, Environment Variables, Azure Key Vault, AWS Secrets Manager.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Company or product name displayed in the application.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Environment stage: 1 = Development, 2 = Staging, 3 = Production
        /// </summary>
        [Range(1, 3, ErrorMessage = "Stage must be 1 (Development), 2 (Staging), or 3 (Production)")]
        public Stage Stage { get; set; }

        /// <summary>
        /// Identity Provider URL (e.g., https://localhost:44303 for development).
        /// Required for authentication to work.
        /// </summary>
        [Required(ErrorMessage = "IDPUrl is required")]
        [Url(ErrorMessage = "IDPUrl must be a valid URL")]
        public string IDPUrl { get; set; }

        /// <summary>
        /// The database connection string. The provider is auto-detected from the connection string format.
        /// Examples:
        /// - SqlServer: "Server=localhost;Database=AuthScape;Trusted_Connection=true;TrustServerCertificate=true;"
        /// - PostgreSQL: "Host=localhost;Database=authscape;Username=postgres;Password=yourpassword"
        /// - MySQL: "Server=localhost;Database=authscape;User=root;Password=yourpassword"
        /// - SQLite: "Data Source=AuthScape.db"
        /// </summary>
        [Required(ErrorMessage = "DatabaseContext connection string is required")]
        public string DatabaseContext { get; set; }

        /// <summary>
        /// Stripe payment gateway configuration.
        /// </summary>
        public StripeAppSetting Stripe { get; set; }

        /// <summary>
        /// SendGrid email service configuration.
        /// </summary>
        public SendGridAppSettings SendGrid { get; set; }

        /// <summary>
        /// Azure Blob Storage configuration for file uploads.
        /// </summary>
        public Storage Storage { get; set; }

        /// <summary>
        /// Document mapping storage configuration.
        /// </summary>
        public Mapping Mapping { get; set; }

        /// <summary>
        /// URL to redirect users after authentication.
        /// </summary>
        [Url(ErrorMessage = "WebsiteRedirectUrl must be a valid URL")]
        public string WebsiteRedirectUrl { get; set; }

        /// <summary>
        /// URL to redirect users after accepting an invite.
        /// </summary>
        [Url(ErrorMessage = "InviteSignupRedirectUrl must be a valid URL")]
        public string InviteSignupRedirectUrl { get; set; }

        /// <summary>
        /// URL to redirect users after login.
        /// </summary>
        [Url(ErrorMessage = "LoginRedirectUrl must be a valid URL")]
        public string LoginRedirectUrl { get; set; }

        /// <summary>
        /// Enable company mode for multi-tenant scenarios.
        /// When true, users are associated with companies.
        /// </summary>
        public bool EnableCompanyMode { get; set; }

        /// <summary>
        /// Ticket system configuration for customer support.
        /// </summary>
        public TicketSystem Ticketing { get; set; }

        /// <summary>
        /// White-label/private label settings.
        /// </summary>
        public PrivateLabelSettings PrivateLabel { get; set; }

        /// <summary>
        /// Azure Form Recognizer document processing settings.
        /// </summary>
        public DocumentProcessing DocumentProcessing { get; set; }

        /// <summary>
        /// Token lifespan in days for password reset and other tokens.
        /// Default: 1 day
        /// </summary>
        [Range(1, 365, ErrorMessage = "Token lifespan must be between 1 and 365 days")]
        public int? DataProtectionTokenProviderOptions_TokenLifespanByDays { get; set; }

        /// <summary>
        /// OpenAI API configuration.
        /// </summary>
        public OpenAI OpenAI { get; set; }

        /// <summary>
        /// Reporting settings.
        /// </summary>
        public ReportingSettings Reporting { get; set; }

        /// <summary>
        /// Lucene search index storage settings.
        /// </summary>
        public LuceneSearch LuceneSearch { get; set; }

        /// <summary>
        /// Azure Vision spreadsheet processing settings.
        /// </summary>
        public Spreadsheet Spreadsheet { get; set; }

        /// <summary>
        /// Subscription feature flags.
        /// </summary>
        public SubscriptionFeatures Subscriptions { get; set; }
    }

    public class OpenAI
    {
        public string APIKey { get; set; }
    }

    public class Mapping
    {
        public string AzureConnectionString { get; set; }
        public string BaseUri { get; set; }
        public string Container { get; set; }
    }

    public class LuceneSearch
    {
        public string StorageConnectionString { get; set; }
        public string Container { get; set; }
    }

    public class Storage
    {
        public string AzureConnectionString { get; set; }
        public string BaseUri { get; set; }
        public string UserProfileContainer { get; set; }
    }

    public class DocumentProcessing
    {
        public string BaseURL { get; set; }
        public string StorageContainer { get; set; }
        public string AzureFormRecognizerEndpoint { get; set; }
        public string AzureFormRecognizerKey { get; set; }
    }

    public class TicketSystem
    {
        public string TemplateId { get; set; }
        public string Domain { get; set; }
        public string Name { get; set; }
        public string Subject { get; set; }
        public TicketSystemAttachments Attachments { get; set; }
    }

    public class TicketSystemAttachments
    {
        public string Container { get; set; }
        public string BaseUri { get; set; }
        public string AzureConnectionString { get; set; }
    }

    public class PrivateLabelSettings
    {
        public string GoogleFontsAPIKey { get; set; }
        public string AppIconContainer { get; set; }



        public string AppServicePlanName { get; set; }
        public string WebAppName { get; set; }
        public string ResourceGroupName { get; set; }
        public string SubscriptionId { get; set; }
        public string OpenIdApplicationId { get; set; }

        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TenantId { get; set; }
    }

    public class ReportingSettings
    {
        public string ProjectName { get; set; }
    }

    public class FormRecognizerSettings
    {
        public string Key { get; set; }
        public string Endpoint { get; set; }
    }

    public class Spreadsheet
    {
        public string AzureVisionKey { get; set; }
        public string AzureVisionEndpoint { get; set; }
    }
}