using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthScape.CRM.Models.Enums;

namespace AuthScape.CRM.Models;

/// <summary>
/// Configures how a CRM entity maps to an AuthScape entity type.
/// For example: Dynamics "contact" → AuthScape User
///              Dynamics "account" → AuthScape Company
///              Dynamics "cr_office" → AuthScape Location
/// </summary>
public class CrmEntityMapping
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// The CRM connection this mapping belongs to
    /// </summary>
    [Required]
    public long CrmConnectionId { get; set; }

    /// <summary>
    /// The CRM entity logical name (e.g., "contact", "account", "cr_office")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string CrmEntityName { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly display name for the CRM entity
    /// </summary>
    [MaxLength(255)]
    public string? CrmEntityDisplayName { get; set; }

    /// <summary>
    /// The AuthScape entity type this CRM entity maps to
    /// </summary>
    [Required]
    public AuthScapeEntityType AuthScapeEntityType { get; set; }

    /// <summary>
    /// Override sync direction for this specific entity mapping
    /// </summary>
    public CrmSyncDirection SyncDirection { get; set; } = CrmSyncDirection.Bidirectional;

    /// <summary>
    /// Whether this mapping is active
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Optional OData/query filter to limit which CRM records sync
    /// For Dynamics: OData filter expression (e.g., "statecode eq 0")
    /// For HubSpot: Filter JSON
    /// </summary>
    public string? CrmFilterExpression { get; set; }

    /// <summary>
    /// The CRM field that uniquely identifies records (usually "id" or entity-specific)
    /// </summary>
    [MaxLength(255)]
    public string CrmPrimaryKeyField { get; set; } = "id";

    /// <summary>
    /// The CRM field used for tracking modifications (e.g., "modifiedon" for Dynamics)
    /// </summary>
    [MaxLength(255)]
    public string? CrmModifiedDateField { get; set; }

    /// <summary>
    /// When this mapping was created
    /// </summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(CrmConnectionId))]
    public virtual CrmConnection? CrmConnection { get; set; }

    public virtual ICollection<CrmFieldMapping> FieldMappings { get; set; } = new List<CrmFieldMapping>();

    public virtual ICollection<CrmRelationshipMapping> RelationshipMappings { get; set; } = new List<CrmRelationshipMapping>();
}
