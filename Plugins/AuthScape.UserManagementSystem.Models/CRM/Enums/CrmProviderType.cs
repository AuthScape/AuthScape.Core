namespace AuthScape.UserManageSystem.Models.CRM.Enums;

/// <summary>
/// Defines the supported CRM provider types
/// </summary>
public enum CrmProviderType
{
    /// <summary>
    /// Microsoft Dynamics 365 / Dataverse
    /// </summary>
    Dynamics365 = 0,

    /// <summary>
    /// HubSpot CRM
    /// </summary>
    HubSpot = 1,

    /// <summary>
    /// Google Contacts / Google Workspace
    /// </summary>
    GoogleContacts = 2,

    /// <summary>
    /// Microsoft Graph / Office 365
    /// </summary>
    MicrosoftGraph = 3,

    /// <summary>
    /// SendGrid Marketing Contacts
    /// </summary>
    SendGridContacts = 4,

    /// <summary>
    /// Salesforce CRM
    /// </summary>
    Salesforce = 5
}
