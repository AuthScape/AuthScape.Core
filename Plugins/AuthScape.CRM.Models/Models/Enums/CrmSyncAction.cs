namespace AuthScape.CRM.Models.Enums;

/// <summary>
/// Defines the action performed during a sync operation
/// </summary>
public enum CrmSyncAction
{
    /// <summary>
    /// A new record was created
    /// </summary>
    Create = 0,

    /// <summary>
    /// An existing record was updated
    /// </summary>
    Update = 1,

    /// <summary>
    /// A record was deleted
    /// </summary>
    Delete = 2
}
