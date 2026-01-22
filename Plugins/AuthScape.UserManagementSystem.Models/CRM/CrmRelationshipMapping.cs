using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthScape.UserManageSystem.Models.CRM.Enums;

namespace AuthScape.UserManageSystem.Models.CRM;

/// <summary>
/// Configures how relationship/lookup fields map between AuthScape entities and CRM entities.
/// For example: AuthScape User.CompanyId → Dynamics Contact.parentcustomerid (Account lookup)
///              AuthScape User.LocationId → Dynamics Contact.new_locationid (Account lookup)
/// </summary>
public class CrmRelationshipMapping
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// The entity mapping this relationship belongs to (e.g., User → Contact mapping)
    /// </summary>
    [Required]
    public long CrmEntityMappingId { get; set; }

    /// <summary>
    /// The AuthScape field that holds the relationship ID (e.g., "CompanyId", "LocationId")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string AuthScapeField { get; set; } = string.Empty;

    /// <summary>
    /// The AuthScape entity type that the field references (e.g., Company, Location)
    /// </summary>
    [Required]
    public AuthScapeEntityType RelatedAuthScapeEntityType { get; set; }

    /// <summary>
    /// The CRM lookup field name (e.g., "parentcustomerid", "_parentcustomerid_value", "new_locationid")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string CrmLookupField { get; set; } = string.Empty;

    /// <summary>
    /// The CRM entity that the lookup references (e.g., "account" for Account lookups)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string CrmRelatedEntityName { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI purposes
    /// </summary>
    [MaxLength(255)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Override sync direction for this specific relationship
    /// </summary>
    public CrmSyncDirection SyncDirection { get; set; } = CrmSyncDirection.Bidirectional;

    /// <summary>
    /// Whether this relationship mapping is active
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether to create the related entity in AuthScape if it doesn't exist during inbound sync
    /// </summary>
    public bool AutoCreateRelated { get; set; } = false;

    /// <summary>
    /// Whether to clear the relationship in CRM if the AuthScape relationship is null
    /// </summary>
    public bool SyncNullValues { get; set; } = true;

    /// <summary>
    /// Display order for UI presentation
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    // Navigation properties
    [ForeignKey(nameof(CrmEntityMappingId))]
    public virtual CrmEntityMapping? CrmEntityMapping { get; set; }
}
