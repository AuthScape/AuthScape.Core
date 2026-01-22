using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthScape.UserManageSystem.Models.CRM.Enums;

namespace AuthScape.UserManageSystem.Models.CRM;

/// <summary>
/// Audit log for CRM sync operations.
/// Tracks every create, update, and delete operation for troubleshooting and compliance.
/// </summary>
public class CrmSyncLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// The CRM connection this log entry belongs to
    /// </summary>
    [Required]
    public long CrmConnectionId { get; set; }

    /// <summary>
    /// The entity mapping used for this sync (optional, for context)
    /// </summary>
    public long? CrmEntityMappingId { get; set; }

    /// <summary>
    /// The AuthScape entity type that was synced
    /// </summary>
    public AuthScapeEntityType? AuthScapeEntityType { get; set; }

    /// <summary>
    /// The AuthScape entity ID that was synced
    /// </summary>
    public long? AuthScapeEntityId { get; set; }

    /// <summary>
    /// The CRM entity logical name
    /// </summary>
    [MaxLength(255)]
    public string? CrmEntityName { get; set; }

    /// <summary>
    /// The CRM record ID
    /// </summary>
    [MaxLength(255)]
    public string? CrmEntityId { get; set; }

    /// <summary>
    /// Direction of this sync operation (Inbound or Outbound)
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Direction { get; set; } = "Outbound";

    /// <summary>
    /// The action performed (Create, Update, Delete)
    /// </summary>
    [Required]
    public CrmSyncAction Action { get; set; }

    /// <summary>
    /// The status of this sync operation
    /// </summary>
    [Required]
    public CrmSyncStatus Status { get; set; }

    /// <summary>
    /// Error message if status is Failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Detailed error information (stack trace, API response, etc.)
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// JSON representation of fields that were changed
    /// </summary>
    public string? ChangedFields { get; set; }

    /// <summary>
    /// Number of records processed (for batch operations)
    /// </summary>
    public int RecordsProcessed { get; set; } = 1;

    /// <summary>
    /// Duration of the sync operation in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// When this sync occurred
    /// </summary>
    public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(CrmConnectionId))]
    public virtual CrmConnection? CrmConnection { get; set; }

    [ForeignKey(nameof(CrmEntityMappingId))]
    public virtual CrmEntityMapping? CrmEntityMapping { get; set; }
}
