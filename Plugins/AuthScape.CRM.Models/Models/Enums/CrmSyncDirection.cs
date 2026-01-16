namespace AuthScape.CRM.Models.Enums;

/// <summary>
/// Defines the direction of data synchronization between AuthScape and CRM
/// </summary>
public enum CrmSyncDirection
{
    /// <summary>
    /// Data flows from CRM to AuthScape only
    /// </summary>
    Inbound = 0,

    /// <summary>
    /// Data flows from AuthScape to CRM only
    /// </summary>
    Outbound = 1,

    /// <summary>
    /// Data flows in both directions
    /// </summary>
    Bidirectional = 2
}
