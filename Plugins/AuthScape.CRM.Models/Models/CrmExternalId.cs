using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthScape.CRM.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace AuthScape.CRM.Models;

/// <summary>
/// Tracks the relationship between AuthScape entities and their CRM counterparts.
/// This enables bidirectional sync by knowing which AuthScape record maps to which CRM record.
/// </summary>
[Index(nameof(CrmConnectionId), nameof(AuthScapeEntityType), nameof(AuthScapeEntityId), IsUnique = true)]
[Index(nameof(CrmConnectionId), nameof(CrmEntityName), nameof(CrmEntityId), IsUnique = true)]
public class CrmExternalId
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// The CRM connection this external ID belongs to
    /// </summary>
    [Required]
    public long CrmConnectionId { get; set; }

    /// <summary>
    /// The AuthScape entity type (User, Company, Location)
    /// </summary>
    [Required]
    public AuthScapeEntityType AuthScapeEntityType { get; set; }

    /// <summary>
    /// The AuthScape entity ID (AppUser.Id, Company.Id, or Location.Id)
    /// </summary>
    [Required]
    public long AuthScapeEntityId { get; set; }

    /// <summary>
    /// The CRM entity logical name (e.g., "contact", "account")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string CrmEntityName { get; set; } = string.Empty;

    /// <summary>
    /// The CRM record ID (GUID for Dynamics, string ID for others)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string CrmEntityId { get; set; } = string.Empty;

    /// <summary>
    /// When this record was last synced
    /// </summary>
    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>
    /// Direction of the last sync (Inbound or Outbound)
    /// </summary>
    [MaxLength(10)]
    public string? LastSyncDirection { get; set; }

    /// <summary>
    /// Hash of the last synced data (for change detection)
    /// </summary>
    [MaxLength(64)]
    public string? LastSyncHash { get; set; }

    /// <summary>
    /// When this external ID was created
    /// </summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(CrmConnectionId))]
    public virtual CrmConnection? CrmConnection { get; set; }
}
