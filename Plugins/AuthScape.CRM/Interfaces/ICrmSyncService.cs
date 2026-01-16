using AuthScape.CRM.Models;
using AuthScape.CRM.Models.Enums;

namespace AuthScape.CRM.Interfaces;

/// <summary>
/// Orchestrates sync operations between AuthScape and CRM systems
/// </summary>
public interface ICrmSyncService
{
    #region Connection Management

    /// <summary>
    /// Gets all CRM connections, optionally filtered by company
    /// </summary>
    Task<IEnumerable<CrmConnection>> GetConnectionsAsync(long? companyId = null);

    /// <summary>
    /// Gets a specific CRM connection by ID
    /// </summary>
    Task<CrmConnection?> GetConnectionAsync(long connectionId);

    /// <summary>
    /// Creates a new CRM connection
    /// </summary>
    Task<CrmConnection> CreateConnectionAsync(CrmConnection connection);

    /// <summary>
    /// Updates an existing CRM connection
    /// </summary>
    Task<CrmConnection> UpdateConnectionAsync(CrmConnection connection);

    /// <summary>
    /// Deletes a CRM connection and all associated mappings
    /// </summary>
    Task<bool> DeleteConnectionAsync(long connectionId);

    /// <summary>
    /// Tests a CRM connection to verify credentials
    /// </summary>
    Task<bool> TestConnectionAsync(long connectionId);

    #endregion

    #region Entity Mapping Management

    /// <summary>
    /// Gets entity mappings for a connection
    /// </summary>
    Task<IEnumerable<CrmEntityMapping>> GetEntityMappingsAsync(long connectionId);

    /// <summary>
    /// Gets a specific entity mapping by ID
    /// </summary>
    Task<CrmEntityMapping?> GetEntityMappingAsync(long mappingId);

    /// <summary>
    /// Creates a new entity mapping
    /// </summary>
    Task<CrmEntityMapping> CreateEntityMappingAsync(CrmEntityMapping mapping);

    /// <summary>
    /// Updates an existing entity mapping
    /// </summary>
    Task<CrmEntityMapping> UpdateEntityMappingAsync(CrmEntityMapping mapping);

    /// <summary>
    /// Deletes an entity mapping and all its field mappings
    /// </summary>
    Task<bool> DeleteEntityMappingAsync(long mappingId);

    /// <summary>
    /// Gets available CRM entities for mapping (schema discovery)
    /// </summary>
    Task<IEnumerable<CrmEntitySchema>> GetAvailableCrmEntitiesAsync(long connectionId);

    /// <summary>
    /// Gets available fields for a CRM entity
    /// </summary>
    Task<IEnumerable<CrmFieldSchema>> GetCrmEntityFieldsAsync(long connectionId, string entityName);

    #endregion

    #region Field Mapping Management

    /// <summary>
    /// Gets field mappings for an entity mapping
    /// </summary>
    Task<IEnumerable<CrmFieldMapping>> GetFieldMappingsAsync(long entityMappingId);

    /// <summary>
    /// Gets a specific field mapping by ID
    /// </summary>
    Task<CrmFieldMapping?> GetFieldMappingAsync(long fieldMappingId);

    /// <summary>
    /// Creates a new field mapping
    /// </summary>
    Task<CrmFieldMapping> CreateFieldMappingAsync(CrmFieldMapping mapping);

    /// <summary>
    /// Updates an existing field mapping
    /// </summary>
    Task<CrmFieldMapping> UpdateFieldMappingAsync(CrmFieldMapping mapping);

    /// <summary>
    /// Deletes a field mapping
    /// </summary>
    Task<bool> DeleteFieldMappingAsync(long fieldMappingId);

    /// <summary>
    /// Gets the available AuthScape fields for an entity type
    /// </summary>
    IEnumerable<string> GetAuthScapeFields(AuthScapeEntityType entityType);

    #endregion

    #region Relationship Mapping Management

    /// <summary>
    /// Gets a specific relationship mapping by ID
    /// </summary>
    Task<CrmRelationshipMapping?> GetRelationshipMappingAsync(long relationshipMappingId);

    /// <summary>
    /// Creates a new relationship mapping
    /// </summary>
    Task<CrmRelationshipMapping> CreateRelationshipMappingAsync(CrmRelationshipMapping mapping);

    /// <summary>
    /// Updates an existing relationship mapping
    /// </summary>
    Task<CrmRelationshipMapping> UpdateRelationshipMappingAsync(CrmRelationshipMapping mapping);

    /// <summary>
    /// Deletes a relationship mapping
    /// </summary>
    Task<bool> DeleteRelationshipMappingAsync(long relationshipMappingId);

    #endregion

    #region Sync Operations

    /// <summary>
    /// Performs a full sync for a connection (both directions based on configuration)
    /// </summary>
    Task<CrmSyncResult> SyncAllAsync(long connectionId);

    /// <summary>
    /// Performs a full sync for a connection with progress callback
    /// </summary>
    /// <param name="connectionId">The connection ID</param>
    /// <param name="progressCallback">Callback invoked with (entityName, currentRecord, totalRecords) for progress tracking</param>
    Task<CrmSyncResult> SyncAllAsync(long connectionId, Action<string, int, int>? progressCallback);

    /// <summary>
    /// Performs an incremental sync (only changes since last sync)
    /// </summary>
    Task<CrmSyncResult> SyncIncrementalAsync(long connectionId);

    /// <summary>
    /// Syncs a specific entity mapping (e.g., just Contact → User)
    /// </summary>
    Task<CrmSyncResult> SyncEntityMappingAsync(long entityMappingId, bool isFullSync = true);

    /// <summary>
    /// Syncs only the relationship mappings for an entity mapping (faster than full sync)
    /// </summary>
    Task<CrmSyncResult> SyncRelationshipsAsync(long entityMappingId);

    /// <summary>
    /// Pulls bcbi_companyid from CRM Contacts and assigns LocationId to AuthScape users
    /// by matching the Location via CrmExternalIds table.
    /// </summary>
    Task<CrmSyncResult> SyncUserLocationsFromCrmAsync(long connectionId);

    /// <summary>
    /// Syncs a specific AuthScape entity to CRM (outbound)
    /// </summary>
    Task<CrmSyncResult> SyncOutboundAsync(long connectionId, AuthScapeEntityType entityType, long entityId);

    /// <summary>
    /// Triggers outbound sync for an entity across ALL active connections that have mappings for this entity type.
    /// Call this when a User/Company/Location is created or updated.
    /// </summary>
    Task<CrmSyncResult> TriggerOutboundSyncAsync(AuthScapeEntityType entityType, long entityId);

    /// <summary>
    /// Syncs a specific CRM record to AuthScape (inbound)
    /// </summary>
    Task<CrmSyncResult> SyncInboundAsync(long connectionId, string crmEntityName, string crmRecordId);

    /// <summary>
    /// Processes an incoming webhook event
    /// </summary>
    Task<CrmSyncResult> ProcessWebhookAsync(long connectionId, CrmWebhookEvent webhookEvent);

    #endregion

    #region Sync Logs & External IDs

    /// <summary>
    /// Gets sync logs for a connection
    /// </summary>
    Task<IEnumerable<CrmSyncLog>> GetSyncLogsAsync(long connectionId, int limit = 100, CrmSyncStatus? status = null, DateTimeOffset? since = null);

    /// <summary>
    /// Clears sync logs for a connection
    /// </summary>
    Task ClearSyncLogsAsync(long connectionId, DateTimeOffset? olderThan = null);

    /// <summary>
    /// Gets external ID mappings for a connection
    /// </summary>
    Task<IEnumerable<CrmExternalId>> GetExternalIdsAsync(long connectionId, AuthScapeEntityType? entityType = null, int limit = 100);

    /// <summary>
    /// Gets sync statistics for a connection
    /// </summary>
    Task<CrmSyncStats> GetSyncStatsAsync(long connectionId, DateTimeOffset? since = null);

    /// <summary>
    /// Gets the AuthScape entity ID for a given CRM record ID
    /// </summary>
    Task<long?> GetAuthScapeEntityIdByCrmIdAsync(long connectionId, string crmEntityName, string crmEntityId);

    /// <summary>
    /// Gets the CRM record ID for a given AuthScape entity
    /// </summary>
    Task<string?> GetCrmIdByAuthScapeEntityAsync(long connectionId, AuthScapeEntityType entityType, long entityId);

    /// <summary>
    /// Gets the external ID mapping for a CRM record
    /// </summary>
    Task<CrmExternalId?> GetExternalIdByCrmIdAsync(long connectionId, string crmEntityName, string crmEntityId);

    /// <summary>
    /// Gets the external ID mapping for an AuthScape entity
    /// </summary>
    Task<CrmExternalId?> GetExternalIdByAuthScapeEntityAsync(long connectionId, AuthScapeEntityType entityType, long entityId);

    /// <summary>
    /// Gets diagnostic information about CRM external ID mappings for debugging sync issues
    /// </summary>
    Task<CrmSyncDiagnostics> GetSyncDiagnosticsAsync(long connectionId);

    #endregion
}

/// <summary>
/// Diagnostic information about CRM sync mappings
/// </summary>
public class CrmSyncDiagnostics
{
    public int TotalUsers { get; set; }
    public int TotalLocations { get; set; }
    public int TotalCompanies { get; set; }
    public int UserToContactMappings { get; set; }
    public int LocationToAccountMappings { get; set; }
    public int CompanyToAccountMappings { get; set; }
    public int ContactsWithCompanyId { get; set; }
    public List<string> SampleUserMappings { get; set; } = new();
    public List<string> SampleAccountMappings { get; set; } = new();
    public string? ConnectionStatus { get; set; }
    public string? Recommendation { get; set; }
}

/// <summary>
/// Result of a sync operation
/// </summary>
public class CrmSyncResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? SyncId { get; set; }
    public CrmSyncStats Stats { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public long DurationMs { get; set; }
}

/// <summary>
/// Sync statistics for dashboard display
/// </summary>
public class CrmSyncStats
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int ConflictCount { get; set; }
    public int SkippedCount { get; set; }
    public int InboundCount { get; set; }
    public int OutboundCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DeletedCount { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }
}
