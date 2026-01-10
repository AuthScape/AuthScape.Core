namespace AuthScape.Configuration.Options;

/// <summary>
/// Options for configuring AuthScape configuration sources.
/// </summary>
public class AuthScapeConfigurationOptions
{
    /// <summary>
    /// Path to shared configuration files. If null, auto-discovers by walking up directory tree.
    /// </summary>
    public string? SharedConfigurationPath { get; set; }

    /// <summary>
    /// Whether to use shared configuration files (authscape.json, authscape.{Environment}.json).
    /// Default: true
    /// </summary>
    public bool UseSharedConfiguration { get; set; } = true;

    /// <summary>
    /// Whether to enable configuration validation at startup.
    /// Default: true
    /// </summary>
    public bool ValidateOnStartup { get; set; } = true;

    /// <summary>
    /// Whether to enable hot reload for JSON configuration files.
    /// Default: true
    /// </summary>
    public bool EnableHotReload { get; set; } = true;

    /// <summary>
    /// Environment variable prefix for AuthScape settings.
    /// Variables should use double underscore for nesting: AUTHSCAPE_AppSettings__DatabaseContext
    /// Default: "AUTHSCAPE_"
    /// </summary>
    public string EnvironmentVariablePrefix { get; set; } = "AUTHSCAPE_";

    /// <summary>
    /// Azure Key Vault configuration for production secrets.
    /// </summary>
    public AzureKeyVaultOptions? AzureKeyVault { get; set; }

    /// <summary>
    /// AWS Secrets Manager configuration for production secrets.
    /// </summary>
    public AwsSecretsManagerOptions? AwsSecretsManager { get; set; }
}

/// <summary>
/// Azure Key Vault configuration options.
/// </summary>
public class AzureKeyVaultOptions
{
    /// <summary>
    /// Whether Azure Key Vault is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The URI of the Azure Key Vault (e.g., https://your-vault.vault.azure.net/).
    /// </summary>
    public string? VaultUri { get; set; }

    /// <summary>
    /// Azure AD Tenant ID. Required when not using managed identity.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Azure AD Application (Client) ID. Required when not using managed identity.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Azure AD Client Secret. Required when not using managed identity.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Whether to use Azure Managed Identity for authentication.
    /// Recommended for production deployments on Azure.
    /// Default: true
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// How often to reload secrets from Key Vault.
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan ReloadInterval { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// AWS Secrets Manager configuration options.
/// </summary>
public class AwsSecretsManagerOptions
{
    /// <summary>
    /// Whether AWS Secrets Manager is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// AWS Region (e.g., us-east-1, eu-west-1).
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// The secret ID or ARN in AWS Secrets Manager.
    /// </summary>
    public string? SecretId { get; set; }

    /// <summary>
    /// AWS Access Key ID. Required when not using default credentials.
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// AWS Secret Access Key. Required when not using default credentials.
    /// </summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>
    /// Whether to use AWS default credentials (IAM role, environment variables, etc.).
    /// Recommended for production deployments on AWS.
    /// Default: true
    /// </summary>
    public bool UseDefaultCredentials { get; set; } = true;
}
