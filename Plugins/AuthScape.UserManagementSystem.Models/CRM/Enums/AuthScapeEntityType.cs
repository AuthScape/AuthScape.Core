namespace AuthScape.UserManageSystem.Models.CRM.Enums;

/// <summary>
/// Defines the AuthScape entity types that can be synced with CRM
/// </summary>
public enum AuthScapeEntityType
{
    /// <summary>
    /// Maps to AppUser entity
    /// </summary>
    User = 0,

    /// <summary>
    /// Maps to Company entity
    /// </summary>
    Company = 1,

    /// <summary>
    /// Maps to Location entity
    /// </summary>
    Location = 2
}
