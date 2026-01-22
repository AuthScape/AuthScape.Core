namespace AuthScape.UserManageSystem.Models.CRM;

/// <summary>
/// Represents metadata about a CRM entity (for schema discovery)
/// </summary>
public class CrmEntitySchema
{
    /// <summary>
    /// The logical/API name of the entity (e.g., "contact", "account")
    /// </summary>
    public string LogicalName { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly display name (e.g., "Contact", "Account")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Plural display name (e.g., "Contacts", "Accounts")
    /// </summary>
    public string? PluralName { get; set; }

    /// <summary>
    /// Description of the entity
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this is a custom entity
    /// </summary>
    public bool IsCustomEntity { get; set; }

    /// <summary>
    /// The primary key field name
    /// </summary>
    public string PrimaryKeyField { get; set; } = "id";

    /// <summary>
    /// The primary name field (used for display)
    /// </summary>
    public string? PrimaryNameField { get; set; }

    /// <summary>
    /// Collection set name for API calls (e.g., "contacts", "accounts")
    /// </summary>
    public string? CollectionName { get; set; }
}

/// <summary>
/// Represents metadata about a CRM entity field
/// </summary>
public class CrmFieldSchema
{
    /// <summary>
    /// The logical/API name of the field
    /// </summary>
    public string LogicalName { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly display name
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the field
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The data type (String, Integer, DateTime, Lookup, OptionSet, etc.)
    /// </summary>
    public string DataType { get; set; } = "String";

    /// <summary>
    /// Whether the field is required
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Whether the field is read-only
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Whether this is a custom field
    /// </summary>
    public bool IsCustomField { get; set; }

    /// <summary>
    /// Maximum length for string fields
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// For Lookup fields: the related entity name
    /// </summary>
    public string? RelatedEntityName { get; set; }

    /// <summary>
    /// For OptionSet fields: available options
    /// </summary>
    public List<CrmOptionSetValue>? Options { get; set; }
}

/// <summary>
/// Represents a value in an option set (picklist/enum)
/// </summary>
public class CrmOptionSetValue
{
    public int Value { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Represents a CRM record (generic dictionary-based)
/// </summary>
public class CrmRecord
{
    /// <summary>
    /// The entity logical name
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// The record ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Field values (field name → value)
    /// </summary>
    public Dictionary<string, object?> Fields { get; set; } = new();

    /// <summary>
    /// When the record was created in the CRM
    /// </summary>
    public DateTimeOffset? CreatedOn { get; set; }

    /// <summary>
    /// When the record was last modified in the CRM
    /// </summary>
    public DateTimeOffset? ModifiedOn { get; set; }
}

/// <summary>
/// Represents a webhook event from a CRM
/// </summary>
public class CrmWebhookEvent
{
    /// <summary>
    /// The type of event (e.g., "create", "update", "delete")
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// The entity name affected
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// The record ID affected
    /// </summary>
    public string RecordId { get; set; } = string.Empty;

    /// <summary>
    /// The record data (may be partial for updates)
    /// </summary>
    public CrmRecord? Record { get; set; }

    /// <summary>
    /// When the event occurred in the CRM
    /// </summary>
    public DateTimeOffset EventTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Raw payload from the CRM (for debugging)
    /// </summary>
    public string? RawPayload { get; set; }
}

/// <summary>
/// Result of an authentication operation
/// </summary>
public class CrmAuthResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
    public string? ErrorMessage => Error ?? ErrorDescription;
}
