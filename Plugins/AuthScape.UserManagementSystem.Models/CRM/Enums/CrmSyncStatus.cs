namespace AuthScape.UserManageSystem.Models.CRM.Enums;

/// <summary>
/// Defines the status of a sync operation
/// </summary>
public enum CrmSyncStatus
{
    /// <summary>
    /// Sync completed successfully
    /// </summary>
    Success = 0,

    /// <summary>
    /// Sync failed with an error
    /// </summary>
    Failed = 1,

    /// <summary>
    /// A conflict was detected (both sides modified)
    /// </summary>
    Conflict = 2,

    /// <summary>
    /// Record was skipped (e.g., filtered out or unchanged)
    /// </summary>
    Skipped = 3,

    /// <summary>
    /// Sync is pending/queued
    /// </summary>
    Pending = 4
}
