using AuthScape.UserManageSystem.Models.CRM;
using AuthScape.UserManageSystem.Models.CRM.Enums;

namespace AuthScape.UserManageSystem.CRM.Interfaces;

/// <summary>
/// Interface for mapping fields between AuthScape entities and CRM records
/// </summary>
public interface ICrmEntityMapper
{
    /// <summary>
    /// Maps an AuthScape entity to CRM fields based on field mappings
    /// </summary>
    Dictionary<string, object?> MapToCrmFields<T>(T entity, IEnumerable<CrmFieldMapping> fieldMappings) where T : class;

    /// <summary>
    /// Maps CRM record fields to an AuthScape entity based on field mappings
    /// </summary>
    void MapFromCrmFields<T>(T entity, CrmRecord crmRecord, IEnumerable<CrmFieldMapping> fieldMappings) where T : class;

    /// <summary>
    /// Gets a dictionary of changed fields between old and new entity states
    /// </summary>
    Dictionary<string, object?> GetChangedFields<T>(T? oldEntity, T newEntity, IEnumerable<CrmFieldMapping> fieldMappings) where T : class;

    /// <summary>
    /// Creates default field mappings for a given AuthScape entity type and CRM provider
    /// </summary>
    IEnumerable<CrmFieldMapping> GetDefaultFieldMappings(AuthScapeEntityType entityType, CrmProviderType providerType);

    /// <summary>
    /// Gets available AuthScape fields for a given entity type
    /// </summary>
    IEnumerable<string> GetAvailableAuthScapeFields(AuthScapeEntityType entityType);
}
