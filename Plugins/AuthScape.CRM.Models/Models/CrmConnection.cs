using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthScape.CRM.Models.Enums;

namespace AuthScape.CRM.Models;

/// <summary>
/// Represents a connection to an external CRM system
/// </summary>
public class CrmConnection
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// Optional: Tenant-specific connection. NULL means system-wide.
    /// </summary>
    public long? CompanyId { get; set; }

    /// <summary>
    /// The type of CRM provider (Dynamics365, HubSpot, etc.)
    /// </summary>
    [Required]
    public CrmProviderType Provider { get; set; }

    /// <summary>
    /// User-friendly name for this connection
    /// </summary>
    [MaxLength(255)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// OAuth access token (encrypted at rest)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// OAuth refresh token (encrypted at rest)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// When the access token expires
    /// </summary>
    public DateTimeOffset? TokenExpiry { get; set; }

    /// <summary>
    /// API key for providers that use key-based auth (e.g., SendGrid)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// OAuth Client ID for the application
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// OAuth Client Secret for the application (encrypted at rest)
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Azure AD Tenant ID (for Microsoft providers)
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Secret for validating webhook signatures
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// Provider-specific environment/instance URL
    /// For Dynamics: https://org.crm.dynamics.com
    /// For HubSpot: https://api.hubapi.com (usually fixed)
    /// </summary>
    [MaxLength(500)]
    public string? EnvironmentUrl { get; set; }

    /// <summary>
    /// Default sync direction for this connection
    /// </summary>
    public CrmSyncDirection SyncDirection { get; set; } = CrmSyncDirection.Bidirectional;

    /// <summary>
    /// How often to run scheduled sync (in minutes)
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Whether this connection is active
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// When the last sync was performed
    /// </summary>
    public DateTimeOffset? LastSyncAt { get; set; }

    /// <summary>
    /// Last sync error message (if any)
    /// </summary>
    public string? LastSyncError { get; set; }

    /// <summary>
    /// When this connection was created
    /// </summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this connection was last updated
    /// </summary>
    public DateTimeOffset? Updated { get; set; }

    // Navigation properties
    public virtual ICollection<CrmEntityMapping> EntityMappings { get; set; } = new List<CrmEntityMapping>();
    public virtual ICollection<CrmExternalId> ExternalIds { get; set; } = new List<CrmExternalId>();
    public virtual ICollection<CrmSyncLog> SyncLogs { get; set; } = new List<CrmSyncLog>();
}
