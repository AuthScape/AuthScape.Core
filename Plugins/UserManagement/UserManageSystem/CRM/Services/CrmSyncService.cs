using System.Diagnostics;
using AuthScape.UserManageSystem.CRM.Interfaces;
using AuthScape.UserManageSystem.Models.CRM;
using AuthScape.UserManageSystem.Models.CRM.Enums;
using AuthScape.Models.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Models;
using Services.Context;

namespace AuthScape.UserManageSystem.CRM.Services;

/// <summary>
/// Orchestrates sync operations between AuthScape and CRM systems
/// </summary>
public class CrmSyncService : ICrmSyncService
{
    private readonly DatabaseContext _context;
    private readonly ICrmProviderFactory _providerFactory;
    private readonly ILogger<CrmSyncService> _logger;
    private readonly ICrmSyncProgressService _progressService;

    public CrmSyncService(
        DatabaseContext context,
        ICrmProviderFactory providerFactory,
        ILogger<CrmSyncService> logger,
        ICrmSyncProgressService progressService)
    {
        _context = context;
        _providerFactory = providerFactory;
        _logger = logger;
        _progressService = progressService;
    }

    #region Connection Management

    public async Task<IEnumerable<CrmConnection>> GetConnectionsAsync(long? companyId = null)
    {
        var query = _context.CrmConnections.AsQueryable();

        if (companyId.HasValue)
        {
            query = query.Where(c => c.CompanyId == companyId || c.CompanyId == null);
        }

        return await query
            .OrderBy(c => c.DisplayName)
            .ToListAsync();
    }

    public async Task<CrmConnection?> GetConnectionAsync(long connectionId)
    {
        return await _context.CrmConnections
            .Include(c => c.EntityMappings)
                .ThenInclude(m => m.FieldMappings)
            .Include(c => c.EntityMappings)
                .ThenInclude(m => m.RelationshipMappings)
            .FirstOrDefaultAsync(c => c.Id == connectionId);
    }

    public async Task<CrmConnection> CreateConnectionAsync(CrmConnection connection)
    {
        connection.Created = DateTimeOffset.UtcNow;
        _context.CrmConnections.Add(connection);
        await _context.SaveChangesAsync();
        return connection;
    }

    public async Task<CrmConnection> UpdateConnectionAsync(CrmConnection connection)
    {
        connection.Updated = DateTimeOffset.UtcNow;
        _context.CrmConnections.Update(connection);
        await _context.SaveChangesAsync();
        return connection;
    }

    public async Task<bool> DeleteConnectionAsync(long connectionId)
    {
        var connection = await _context.CrmConnections.FindAsync(connectionId);
        if (connection != null)
        {
            _context.CrmConnections.Remove(connection);
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public async Task<bool> TestConnectionAsync(long connectionId)
    {
        var connection = await GetConnectionAsync(connectionId);
        if (connection == null) return false;

        try
        {
            var provider = _providerFactory.GetProvider(connection);
            return await provider.ValidateConnectionAsync(connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test CRM connection {ConnectionId}", connectionId);
            return false;
        }
    }

    #endregion

    #region Entity Mapping Management

    public async Task<IEnumerable<CrmEntityMapping>> GetEntityMappingsAsync(long connectionId)
    {
        return await _context.CrmEntityMappings
            .Include(m => m.FieldMappings)
            .Include(m => m.RelationshipMappings)
            .Where(m => m.CrmConnectionId == connectionId)
            .OrderBy(m => m.CrmEntityDisplayName)
            .ToListAsync();
    }

    public async Task<CrmEntityMapping?> GetEntityMappingAsync(long mappingId)
    {
        return await _context.CrmEntityMappings
            .Include(m => m.FieldMappings)
            .Include(m => m.RelationshipMappings)
            .FirstOrDefaultAsync(m => m.Id == mappingId);
    }

    public async Task<CrmEntityMapping> CreateEntityMappingAsync(CrmEntityMapping mapping)
    {
        mapping.Created = DateTimeOffset.UtcNow;
        _context.CrmEntityMappings.Add(mapping);
        await _context.SaveChangesAsync();
        return mapping;
    }

    public async Task<CrmEntityMapping> UpdateEntityMappingAsync(CrmEntityMapping mapping)
    {
        _context.CrmEntityMappings.Update(mapping);
        await _context.SaveChangesAsync();
        return mapping;
    }

    public async Task<bool> DeleteEntityMappingAsync(long mappingId)
    {
        var mapping = await _context.CrmEntityMappings.FindAsync(mappingId);
        if (mapping != null)
        {
            _context.CrmEntityMappings.Remove(mapping);
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public async Task<IEnumerable<CrmEntitySchema>> GetAvailableCrmEntitiesAsync(long connectionId)
    {
        var connection = await GetConnectionAsync(connectionId);
        if (connection == null) return Enumerable.Empty<CrmEntitySchema>();

        var provider = _providerFactory.GetProvider(connection);
        return await provider.GetAvailableEntitiesAsync(connection);
    }

    public async Task<IEnumerable<CrmFieldSchema>> GetCrmEntityFieldsAsync(long connectionId, string entityName)
    {
        var connection = await GetConnectionAsync(connectionId);
        if (connection == null) return Enumerable.Empty<CrmFieldSchema>();

        var provider = _providerFactory.GetProvider(connection);
        return await provider.GetEntityFieldsAsync(connection, entityName);
    }

    #endregion

    #region Field Mapping Management

    public async Task<IEnumerable<CrmFieldMapping>> GetFieldMappingsAsync(long entityMappingId)
    {
        return await _context.CrmFieldMappings
            .Where(m => m.CrmEntityMappingId == entityMappingId)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync();
    }

    public async Task<CrmFieldMapping?> GetFieldMappingAsync(long fieldMappingId)
    {
        return await _context.CrmFieldMappings.FindAsync(fieldMappingId);
    }

    public async Task<CrmFieldMapping> CreateFieldMappingAsync(CrmFieldMapping mapping)
    {
        _context.CrmFieldMappings.Add(mapping);
        await _context.SaveChangesAsync();
        return mapping;
    }

    public async Task<CrmFieldMapping> UpdateFieldMappingAsync(CrmFieldMapping mapping)
    {
        _context.CrmFieldMappings.Update(mapping);
        await _context.SaveChangesAsync();
        return mapping;
    }

    public async Task<bool> DeleteFieldMappingAsync(long fieldMappingId)
    {
        var mapping = await _context.CrmFieldMappings.FindAsync(fieldMappingId);
        if (mapping != null)
        {
            _context.CrmFieldMappings.Remove(mapping);
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public IEnumerable<string> GetAuthScapeFields(AuthScapeEntityType entityType)
    {
        return entityType switch
        {
            AuthScapeEntityType.User => new[]
            {
                "FirstName", "LastName", "Email", "PhoneNumber", "PhotoUri",
                "locale", "Culture", "Country", "TimeZoneId",
                "IsActive", "Created", "LastLoggedIn"
            },
            AuthScapeEntityType.Company => new[]
            {
                "Title", "Logo", "Description", "IsDeactivated"
            },
            AuthScapeEntityType.Location => new[]
            {
                "Title", "Address", "City", "State", "ZipCode",
                "lat", "lng", "IsDeactivated"
            },
            _ => Enumerable.Empty<string>()
        };
    }

    #endregion

    #region Relationship Mapping Management

    public async Task<CrmRelationshipMapping?> GetRelationshipMappingAsync(long relationshipMappingId)
    {
        return await _context.CrmRelationshipMappings.FindAsync(relationshipMappingId);
    }

    public async Task<CrmRelationshipMapping> CreateRelationshipMappingAsync(CrmRelationshipMapping mapping)
    {
        _context.CrmRelationshipMappings.Add(mapping);
        await _context.SaveChangesAsync();
        return mapping;
    }

    public async Task<CrmRelationshipMapping> UpdateRelationshipMappingAsync(CrmRelationshipMapping mapping)
    {
        _context.CrmRelationshipMappings.Update(mapping);
        await _context.SaveChangesAsync();
        return mapping;
    }

    public async Task<bool> DeleteRelationshipMappingAsync(long relationshipMappingId)
    {
        var mapping = await _context.CrmRelationshipMappings.FindAsync(relationshipMappingId);
        if (mapping != null)
        {
            _context.CrmRelationshipMappings.Remove(mapping);
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }

    #endregion

    #region Sync Operations

    public async Task<CrmSyncResult> SyncAllAsync(long connectionId)
    {
        return await SyncAllAsync(connectionId, null);
    }

    public async Task<CrmSyncResult> SyncAllAsync(long connectionId, Action<string, int, int>? progressCallback)
    {
        var result = new CrmSyncResult();
        var sw = Stopwatch.StartNew();

        try
        {
            var connection = await GetConnectionAsync(connectionId);
            if (connection == null || !connection.IsEnabled)
            {
                result.Errors.Add("Connection not found or disabled");
                result.Message = "Connection not found or disabled";
                return result;
            }

            var mappings = await GetEntityMappingsAsync(connectionId);
            var enabledMappings = mappings.Where(m => m.IsEnabled).ToList();

            foreach (var mapping in enabledMappings)
            {
                // Pass isFullSync=true to fetch ALL records from CRM (not just modified since last sync)
                var mappingResult = await SyncEntityMappingWithProgressAsync(connection, mapping, isFullSync: true, progressCallback);
                result.Stats.TotalProcessed += mappingResult.Stats.TotalProcessed;
                result.Stats.CreatedCount += mappingResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += mappingResult.Stats.UpdatedCount;
                result.Stats.DeletedCount += mappingResult.Stats.DeletedCount;
                result.Stats.FailedCount += mappingResult.Stats.FailedCount;
                result.Stats.SkippedCount += mappingResult.Stats.SkippedCount;
                result.Stats.InboundCount += mappingResult.Stats.InboundCount;
                result.Stats.OutboundCount += mappingResult.Stats.OutboundCount;
                result.Errors.AddRange(mappingResult.Errors);
            }

            // Update last sync time
            connection.LastSyncAt = DateTimeOffset.UtcNow;
            connection.LastSyncError = result.Errors.Any() ? string.Join("; ", result.Errors.Take(3)) : null;
            await UpdateConnectionAsync(connection);

            result.Success = !result.Errors.Any();
            result.Stats.SuccessCount = result.Stats.TotalProcessed - result.Stats.FailedCount;
            result.Stats.LastSyncAt = connection.LastSyncAt;
            result.Message = result.Success ? "Sync completed successfully" : "Sync completed with errors";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during full sync for connection {ConnectionId}", connectionId);
            result.Errors.Add(ex.Message);
            result.Message = ex.Message;
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async Task<CrmSyncResult> SyncIncrementalAsync(long connectionId)
    {
        var result = new CrmSyncResult();
        var sw = Stopwatch.StartNew();

        try
        {
            var connection = await GetConnectionAsync(connectionId);
            if (connection == null || !connection.IsEnabled)
            {
                result.Errors.Add("Connection not found or disabled");
                result.Message = "Connection not found or disabled";
                return result;
            }

            var mappings = await GetEntityMappingsAsync(connectionId);

            foreach (var mapping in mappings.Where(m => m.IsEnabled))
            {
                // Pass isFullSync=false to only fetch records modified since last sync
                var mappingResult = await SyncEntityMappingAsync(connection, mapping, isFullSync: false);
                result.Stats.TotalProcessed += mappingResult.Stats.TotalProcessed;
                result.Stats.CreatedCount += mappingResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += mappingResult.Stats.UpdatedCount;
                result.Stats.DeletedCount += mappingResult.Stats.DeletedCount;
                result.Stats.FailedCount += mappingResult.Stats.FailedCount;
                result.Stats.SkippedCount += mappingResult.Stats.SkippedCount;
                result.Stats.InboundCount += mappingResult.Stats.InboundCount;
                result.Stats.OutboundCount += mappingResult.Stats.OutboundCount;
                result.Errors.AddRange(mappingResult.Errors);
            }

            // Update last sync time
            connection.LastSyncAt = DateTimeOffset.UtcNow;
            connection.LastSyncError = result.Errors.Any() ? string.Join("; ", result.Errors.Take(3)) : null;
            await UpdateConnectionAsync(connection);

            result.Success = !result.Errors.Any();
            result.Stats.SuccessCount = result.Stats.TotalProcessed - result.Stats.FailedCount;
            result.Stats.LastSyncAt = connection.LastSyncAt;
            result.Message = result.Success ? "Incremental sync completed successfully" : "Incremental sync completed with errors";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during incremental sync for connection {ConnectionId}", connectionId);
            result.Errors.Add(ex.Message);
            result.Message = ex.Message;
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async Task<CrmSyncResult> SyncEntityMappingAsync(long entityMappingId, bool isFullSync = true)
    {
        var result = new CrmSyncResult();
        var sw = Stopwatch.StartNew();
        string? syncId = null;

        try
        {
            var mapping = await GetEntityMappingAsync(entityMappingId);
            if (mapping == null)
            {
                result.Errors.Add("Entity mapping not found");
                result.Message = "Entity mapping not found";
                return result;
            }

            var connection = await GetConnectionAsync(mapping.CrmConnectionId);
            if (connection == null || !connection.IsEnabled)
            {
                result.Errors.Add("Connection not found or disabled");
                result.Message = "Connection not found or disabled";
                return result;
            }

            if (!mapping.IsEnabled)
            {
                result.Errors.Add("Entity mapping is disabled");
                result.Message = "Entity mapping is disabled";
                return result;
            }

            _logger.LogInformation("Starting sync for entity mapping {MappingId}: {CrmEntity} -> {AuthScapeEntity}",
                entityMappingId, mapping.CrmEntityName, mapping.AuthScapeEntityType);

            // Count total records for progress tracking
            var totalRecords = await CountRecordsForSyncAsync(connection, mapping, isFullSync);

            // Start progress tracking
            syncId = _progressService.StartSync(entityMappingId, mapping.CrmEntityName, totalRecords);
            result.SyncId = syncId;

            result = await SyncEntityMappingWithProgressAsync(connection, mapping, isFullSync, syncId);
            result.SyncId = syncId;

            result.Success = !result.Errors.Any();
            result.Stats.SuccessCount = result.Stats.TotalProcessed - result.Stats.FailedCount;
            result.Message = result.Success
                ? $"Sync completed successfully for {mapping.CrmEntityName}"
                : $"Sync completed with errors for {mapping.CrmEntityName}";

            // Complete progress tracking
            if (syncId != null)
            {
                await _progressService.CompleteSyncAsync(syncId, result.Success, result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync for entity mapping {MappingId}", entityMappingId);
            result.Errors.Add(ex.Message);
            result.Message = ex.Message;

            // Report failure to progress service
            if (syncId != null)
            {
                await _progressService.CompleteSyncAsync(syncId, false, ex.Message);
            }
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async Task<CrmSyncResult> SyncRelationshipsAsync(long entityMappingId)
    {
        var result = new CrmSyncResult();
        var sw = Stopwatch.StartNew();
        string? syncId = null;

        try
        {
            var mapping = await GetEntityMappingAsync(entityMappingId);
            if (mapping == null)
            {
                result.Errors.Add("Entity mapping not found");
                result.Message = "Entity mapping not found";
                return result;
            }

            var connection = await GetConnectionAsync(mapping.CrmConnectionId);
            if (connection == null || !connection.IsEnabled)
            {
                result.Errors.Add("Connection not found or disabled");
                result.Message = "Connection not found or disabled";
                return result;
            }

            if (!mapping.IsEnabled)
            {
                result.Errors.Add("Entity mapping is disabled");
                result.Message = "Entity mapping is disabled";
                return result;
            }

            var relationshipMappings = mapping.RelationshipMappings?.Where(r => r.IsEnabled).ToList() ?? new List<CrmRelationshipMapping>();
            if (!relationshipMappings.Any())
            {
                result.Message = "No enabled relationship mappings found";
                result.Success = true;
                return result;
            }

            _logger.LogInformation("Starting relationship sync for entity mapping {MappingId}: {Count} relationships",
                entityMappingId, relationshipMappings.Count);

            var provider = _providerFactory.GetProvider(connection);

            // Get all AuthScape entities of this type that have external IDs (already synced)
            var externalIds = await _context.CrmExternalIds
                .Where(e => e.CrmConnectionId == connection.Id &&
                           e.AuthScapeEntityType == mapping.AuthScapeEntityType &&
                           e.CrmEntityName == mapping.CrmEntityName)
                .ToListAsync();

            _logger.LogInformation("Found {Count} synced records to update relationships for", externalIds.Count);

            // Start progress tracking
            syncId = _progressService.StartSync(entityMappingId, $"{mapping.CrmEntityName} Relationships", externalIds.Count);
            result.SyncId = syncId;

            var processedCount = 0;
            _logger.LogInformation("Starting to process {Count} external IDs with {RelCount} relationship mappings",
                externalIds.Count, relationshipMappings.Count);

            foreach (var externalId in externalIds)
            {
                try
                {
                    // Build only the relationship field updates
                    var relationshipFields = new Dictionary<string, object?>();

                    foreach (var relMapping in relationshipMappings)
                    {
                        // Auto-resolve the correct lookup field from CRM metadata
                        var lookupField = await ResolveLookupFieldAsync(
                            provider, connection, mapping.CrmEntityName, relMapping);

                        if (string.IsNullOrEmpty(lookupField))
                        {
                            _logger.LogWarning("Could not resolve lookup field for relationship {AuthScapeField} → {CrmRelatedEntity} on {CrmEntity}",
                                relMapping.AuthScapeField, relMapping.CrmRelatedEntityName, mapping.CrmEntityName);
                            continue;
                        }

                        _logger.LogDebug("Processing relationship mapping: AuthScapeField={AuthScapeField}, RelatedEntityType={RelatedEntityType}, CrmLookupField={CrmLookupField}, CrmRelatedEntityName={CrmRelatedEntityName}",
                            relMapping.AuthScapeField, relMapping.RelatedAuthScapeEntityType, lookupField, relMapping.CrmRelatedEntityName);

                        // Get the related entity's CRM ID
                        var relatedCrmId = await ResolveRelationshipValueAsync(
                            connection,
                            mapping.AuthScapeEntityType,
                            externalId.AuthScapeEntityId,
                            relMapping);

                        _logger.LogDebug("ResolveRelationshipValueAsync returned: {RelatedCrmId} for AuthScapeEntityId={AuthScapeEntityId}",
                            relatedCrmId ?? "(null)", externalId.AuthScapeEntityId);

                        if (relatedCrmId != null)
                        {
                            // Use @odata.bind format for Dynamics lookup fields
                            relationshipFields[$"{lookupField}@odata.bind"] = relatedCrmId;
                            _logger.LogDebug("Added relationship field: {Field}={Value}",
                                $"{lookupField}@odata.bind", relatedCrmId);
                        }
                        else if (relMapping.SyncNullValues)
                        {
                            // Clear the lookup field using @odata.bind null annotation
                            relationshipFields[$"{lookupField}@odata.bind"] = null;
                        }
                    }

                    if (relationshipFields.Any())
                    {
                        await provider.UpdateRecordAsync(connection, mapping.CrmEntityName, externalId.CrmEntityId, relationshipFields);
                        result.Stats.UpdatedCount++;
                        _logger.LogDebug("Updated relationships for {EntityType} {EntityId} -> {CrmEntity} {CrmId}",
                            mapping.AuthScapeEntityType, externalId.AuthScapeEntityId, mapping.CrmEntityName, externalId.CrmEntityId);
                    }
                    else
                    {
                        // No relationship fields could be resolved - track as skipped
                        result.Stats.SkippedCount++;
                        _logger.LogDebug("Skipped {EntityType} {EntityId} - no relationship values could be resolved. " +
                            "Make sure the related entities (e.g., Locations) have been synced to CRM first.",
                            mapping.AuthScapeEntityType, externalId.AuthScapeEntityId);
                    }

                    result.Stats.TotalProcessed++;
                    processedCount++;

                    // Report progress every 10 records or at key milestones to avoid flooding SignalR
                    if (syncId != null && (processedCount % 10 == 0 || processedCount == 1 || processedCount == externalIds.Count))
                    {
                        await _progressService.ReportProgressAsync(syncId, processedCount, $"Processing record {processedCount} of {externalIds.Count}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update relationships for {CrmEntity} {CrmId}",
                        mapping.CrmEntityName, externalId.CrmEntityId);
                    result.Stats.FailedCount++;
                    result.Stats.TotalProcessed++;
                    processedCount++;
                    result.Errors.Add($"Failed to update {externalId.CrmEntityId}: {ex.Message}");

                    // Report progress even on failure so the UI shows movement
                    if (syncId != null && (processedCount % 10 == 0 || processedCount == 1 || processedCount == externalIds.Count))
                    {
                        await _progressService.ReportProgressAsync(syncId, processedCount, $"Processing record {processedCount} of {externalIds.Count} (with errors)");
                    }
                }
            }

            result.Success = result.Stats.FailedCount == 0;
            result.Stats.SuccessCount = result.Stats.TotalProcessed - result.Stats.FailedCount - result.Stats.SkippedCount;

            // Build detailed message
            var messageParts = new List<string>();
            if (result.Stats.UpdatedCount > 0)
                messageParts.Add($"{result.Stats.UpdatedCount} updated");
            if (result.Stats.SkippedCount > 0)
                messageParts.Add($"{result.Stats.SkippedCount} skipped (related entities not synced)");
            if (result.Stats.FailedCount > 0)
                messageParts.Add($"{result.Stats.FailedCount} failed");

            result.Message = messageParts.Any()
                ? $"Relationship sync completed: {string.Join(", ", messageParts)}"
                : "Relationship sync completed: no records to process";

            // Complete progress tracking
            if (syncId != null)
            {
                await _progressService.CompleteSyncAsync(syncId, result.Success, result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during relationship sync for entity mapping {MappingId}", entityMappingId);
            result.Errors.Add(ex.Message);
            result.Message = ex.Message;

            if (syncId != null)
            {
                await _progressService.CompleteSyncAsync(syncId, false, ex.Message);
            }
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    /// <summary>
    /// Pulls location/account assignments from CRM Contacts and assigns LocationId to AuthScape users.
    /// Uses the configured relationship mapping to determine which CRM field contains the account reference.
    /// Auto-creates Location → Account mappings if a matching Location is found by name.
    /// Optionally creates a Company from the Location's title when autoCreateCompany is true.
    /// </summary>
    public async Task<CrmSyncResult> SyncUserLocationsFromCrmAsync(long connectionId, bool autoCreateCompany = false)
    {
        var companiesCreated = 0;
        var result = new CrmSyncResult();
        var sw = Stopwatch.StartNew();
        string? syncId = null;

        try
        {
            var connection = await GetConnectionAsync(connectionId);
            if (connection == null || !connection.IsEnabled)
            {
                result.Errors.Add("Connection not found or disabled");
                result.Message = "Connection not found or disabled";
                return result;
            }

            var provider = _providerFactory.GetProvider(connection);

            // Find the User → Contact entity mapping with relationship mappings
            var userContactMapping = await _context.CrmEntityMappings
                .Include(m => m.RelationshipMappings)
                .FirstOrDefaultAsync(m => m.CrmConnectionId == connectionId &&
                                         m.AuthScapeEntityType == AuthScapeEntityType.User &&
                                         m.CrmEntityName == "contact");

            // Get configured relationship mapping for Location → Account
            var locationRelMapping = userContactMapping?.RelationshipMappings?
                .FirstOrDefault(r => r.IsEnabled &&
                    r.RelatedAuthScapeEntityType == AuthScapeEntityType.Location &&
                    r.CrmRelatedEntityName == "account");

            // Build list of field names to check - prioritize configured mapping
            var possibleFieldNames = new List<string>();
            if (locationRelMapping != null)
            {
                var mappedField = locationRelMapping.CrmLookupField;
                possibleFieldNames.Add($"_{mappedField}_value");  // Dynamics lookup value format
                possibleFieldNames.Add(mappedField);
                _logger.LogInformation("Using configured relationship mapping field: {Field}", mappedField);
            }
            // Always include standard fallbacks
            possibleFieldNames.AddRange(new[]
            {
                "_parentcustomerid_value",
                "parentcustomerid",
                "_bcbi_companyid_value",
                "bcbi_companyid"
            });

            // Get all User → Contact mappings
            var userExternalIds = await _context.CrmExternalIds
                .Where(e => e.CrmConnectionId == connectionId &&
                           e.AuthScapeEntityType == AuthScapeEntityType.User &&
                           e.CrmEntityName == "contact")
                .ToListAsync();

            _logger.LogInformation("Starting user location sync from CRM for {Count} users", userExternalIds.Count);

            // Start progress tracking with connection ID for SignalR grouping
            syncId = _progressService.StartSyncWithConnection(0, connectionId, "User Location Sync from CRM", userExternalIds.Count);
            result.SyncId = syncId;

            var processedCount = 0;
            var updatedCount = 0;
            var skippedCount = 0;
            var autoCreatedMappings = 0;

            foreach (var userExternalId in userExternalIds)
            {
                try
                {
                    // Fetch Contact from CRM
                    var contactRecord = await provider.GetRecordAsync(connection, "contact", userExternalId.CrmEntityId);
                    if (contactRecord == null)
                    {
                        _logger.LogWarning("Contact not found in CRM for user {UserId}, CrmEntityId={CrmEntityId}",
                            userExternalId.AuthScapeEntityId, userExternalId.CrmEntityId);
                        skippedCount++;
                        processedCount++;
                        result.Stats.TotalProcessed++;
                        continue;
                    }

                    // Log all fields from the contact to help debug
                    if (processedCount < 3) // Log first 3 contacts for debugging
                    {
                        _logger.LogInformation("Contact {CrmEntityId} fields: {Fields}",
                            userExternalId.CrmEntityId,
                            string.Join(", ", contactRecord.Fields.Keys));
                    }

                    // Get account GUID from configured or fallback fields
                    string? accountGuid = null;
                    string? foundFieldName = null;
                    foreach (var fieldName in possibleFieldNames)
                    {
                        if (contactRecord.Fields.TryGetValue(fieldName, out var fieldValue) && fieldValue != null)
                        {
                            var valueStr = fieldValue.ToString();
                            if (!string.IsNullOrEmpty(valueStr))
                            {
                                accountGuid = valueStr;
                                foundFieldName = fieldName;
                                _logger.LogDebug("Found account GUID in field {FieldName}: {Value}", fieldName, accountGuid);
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(accountGuid))
                    {
                        if (processedCount < 10) // Log first 10 skips
                        {
                            _logger.LogWarning("Contact {ContactId} has no account reference set. Checked fields: {Fields}. Available account-related fields: {AvailableFields}",
                                userExternalId.CrmEntityId,
                                string.Join(", ", possibleFieldNames),
                                string.Join(", ", contactRecord.Fields.Where(f =>
                                    f.Key.Contains("company", StringComparison.OrdinalIgnoreCase) ||
                                    f.Key.Contains("parent", StringComparison.OrdinalIgnoreCase) ||
                                    f.Key.Contains("account", StringComparison.OrdinalIgnoreCase))
                                    .Select(f => $"{f.Key}={f.Value}")));
                        }
                        skippedCount++;
                        processedCount++;
                        result.Stats.TotalProcessed++;
                        continue;
                    }

                    // Find Location by account GUID - check both Location and Company entity types
                    var locationExternalId = await _context.CrmExternalIds
                        .Where(e => e.CrmConnectionId == connectionId &&
                                   (e.AuthScapeEntityType == AuthScapeEntityType.Location || e.AuthScapeEntityType == AuthScapeEntityType.Company) &&
                                   e.CrmEntityName == "account" &&
                                   e.CrmEntityId == accountGuid)
                        .FirstOrDefaultAsync();

                    // If no mapping exists, try to auto-create one by fetching the account from CRM and matching by name
                    if (locationExternalId == null)
                    {
                        locationExternalId = await TryAutoCreateLocationAccountMappingAsync(
                            connection, provider, connectionId, accountGuid);

                        if (locationExternalId != null)
                        {
                            autoCreatedMappings++;
                            _logger.LogInformation("Auto-created Location→Account mapping for account {AccountGuid}", accountGuid);
                        }
                    }

                    if (locationExternalId == null)
                    {
                        if (processedCount < 10)
                        {
                            _logger.LogWarning("No Location/Company found for account GUID {AccountGuid} (from field {Field}). " +
                                "No matching Location by name either.",
                                accountGuid, foundFieldName);
                        }
                        skippedCount++;
                        processedCount++;
                        result.Stats.TotalProcessed++;
                        continue;
                    }

                    // Update User's LocationId
                    var user = await _context.Users.FindAsync(userExternalId.AuthScapeEntityId);
                    if (user != null)
                    {
                        var locationChanged = user.LocationId != locationExternalId.AuthScapeEntityId;
                        if (locationChanged)
                        {
                            user.LocationId = locationExternalId.AuthScapeEntityId;
                            updatedCount++;
                            _logger.LogInformation("Updated User {UserId} ({Email}) LocationId to {LocationId}",
                                user.Id, user.Email, locationExternalId.AuthScapeEntityId);
                        }
                        else
                        {
                            _logger.LogDebug("User {UserId} already has LocationId {LocationId}",
                                user.Id, user.LocationId);
                        }

                        // Auto-create Company from Location if enabled
                        if (autoCreateCompany && locationExternalId.AuthScapeEntityType == AuthScapeEntityType.Location)
                        {
                            var location = await _context.Locations.FindAsync(locationExternalId.AuthScapeEntityId);
                            if (location != null && !string.IsNullOrEmpty(location.Title))
                            {
                                // Check if a company with this name already exists
                                var existingCompany = await _context.Companies
                                    .FirstOrDefaultAsync(c => c.Title != null && c.Title.ToLower() == location.Title.ToLower());

                                if (existingCompany == null)
                                {
                                    // Create new company with the location's title
                                    var newCompany = new Company
                                    {
                                        Title = location.Title
                                    };
                                    _context.Companies.Add(newCompany);
                                    await _context.SaveChangesAsync();

                                    // Update the location to reference this company
                                    location.CompanyId = newCompany.Id;

                                    // Update the user's company
                                    user.CompanyId = newCompany.Id;

                                    companiesCreated++;
                                    _logger.LogInformation("Auto-created Company '{CompanyName}' (ID:{CompanyId}) from Location '{LocationName}'",
                                        newCompany.Title, newCompany.Id, location.Title);
                                }
                                else
                                {
                                    // Update location and user to reference existing company
                                    if (location.CompanyId != existingCompany.Id)
                                    {
                                        location.CompanyId = existingCompany.Id;
                                    }
                                    if (user.CompanyId != existingCompany.Id)
                                    {
                                        user.CompanyId = existingCompany.Id;
                                    }
                                    _logger.LogDebug("Using existing Company '{CompanyName}' (ID:{CompanyId}) for Location '{LocationName}'",
                                        existingCompany.Title, existingCompany.Id, location.Title);
                                }
                            }
                        }

                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync location for user {UserId}", userExternalId.AuthScapeEntityId);
                    result.Stats.FailedCount++;
                    result.Errors.Add($"User {userExternalId.AuthScapeEntityId}: {ex.Message}");
                }

                processedCount++;
                result.Stats.TotalProcessed++;

                // Report progress
                if (syncId != null && (processedCount % 10 == 0 || processedCount == 1 || processedCount == userExternalIds.Count))
                {
                    await _progressService.ReportProgressAsync(syncId, processedCount,
                        $"Processing {processedCount} of {userExternalIds.Count}");
                }
            }

            result.Stats.UpdatedCount = updatedCount;
            result.Stats.SkippedCount = skippedCount;
            result.Stats.SuccessCount = updatedCount;
            result.Stats.CreatedCount = autoCreatedMappings + companiesCreated; // Track auto-created mappings and companies
            result.Success = result.Stats.FailedCount == 0;

            var messageBuilder = $"Sync completed: {updatedCount} users updated, {skippedCount} skipped";
            if (autoCreatedMappings > 0) messageBuilder += $", {autoCreatedMappings} mappings auto-created";
            if (companiesCreated > 0) messageBuilder += $", {companiesCreated} companies created";
            if (result.Stats.FailedCount > 0) messageBuilder += $", {result.Stats.FailedCount} failed";
            result.Message = messageBuilder;

            _logger.LogInformation("User location sync completed: {Updated} updated, {Skipped} skipped, {AutoCreated} mappings auto-created, {Companies} companies created, {Failed} failed",
                updatedCount, skippedCount, autoCreatedMappings, companiesCreated, result.Stats.FailedCount);

            if (syncId != null)
            {
                await _progressService.CompleteSyncAsync(syncId, result.Success, result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user location sync from CRM");
            result.Errors.Add(ex.Message);
            result.Message = ex.Message;

            if (syncId != null)
            {
                await _progressService.CompleteSyncAsync(syncId, false, ex.Message);
            }
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    /// <summary>
    /// Attempts to auto-create a Location → Account mapping by fetching the account from CRM
    /// and matching it to an existing Location by name.
    /// </summary>
    private async Task<CrmExternalId?> TryAutoCreateLocationAccountMappingAsync(
        CrmConnection connection,
        ICrmProvider provider,
        long connectionId,
        string accountGuid)
    {
        try
        {
            // Fetch the account from CRM to get its name
            var accountRecord = await provider.GetRecordAsync(connection, "account", accountGuid);
            if (accountRecord == null)
            {
                _logger.LogDebug("Account {AccountGuid} not found in CRM", accountGuid);
                return null;
            }

            // Get the account name
            var accountName = accountRecord.Fields.GetValueOrDefault("name")?.ToString();
            if (string.IsNullOrEmpty(accountName))
            {
                _logger.LogDebug("Account {AccountGuid} has no name", accountGuid);
                return null;
            }

            // Try to find a matching Location by title (case-insensitive)
            var matchingLocation = await _context.Locations
                .Where(l => l.Title != null && l.Title.ToLower() == accountName.ToLower())
                .FirstOrDefaultAsync();

            if (matchingLocation == null)
            {
                _logger.LogDebug("No Location found matching account name '{AccountName}'", accountName);
                return null;
            }

            // Check if this Location already has a different CRM mapping
            var existingMapping = await _context.CrmExternalIds
                .FirstOrDefaultAsync(e => e.CrmConnectionId == connectionId &&
                                         e.AuthScapeEntityType == AuthScapeEntityType.Location &&
                                         e.AuthScapeEntityId == matchingLocation.Id);

            if (existingMapping != null)
            {
                _logger.LogDebug("Location {LocationId} already has a CRM mapping to {CrmEntityId}",
                    matchingLocation.Id, existingMapping.CrmEntityId);
                return null;
            }

            // Create the mapping
            var newExternalId = new CrmExternalId
            {
                CrmConnectionId = connectionId,
                AuthScapeEntityType = AuthScapeEntityType.Location,
                AuthScapeEntityId = matchingLocation.Id,
                CrmEntityName = "account",
                CrmEntityId = accountGuid,
                LastSyncedAt = DateTimeOffset.UtcNow,
                LastSyncDirection = "AutoCreated",
                Created = DateTimeOffset.UtcNow
            };

            _context.CrmExternalIds.Add(newExternalId);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Auto-created Location→Account mapping: Location '{LocationName}' (ID:{LocationId}) → Account '{AccountName}' (GUID:{AccountGuid})",
                matchingLocation.Title, matchingLocation.Id, accountName, accountGuid);

            return newExternalId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error auto-creating Location→Account mapping for account {AccountGuid}", accountGuid);
            return null;
        }
    }

    public async Task<CrmSyncResult> SyncOutboundAsync(long connectionId, AuthScapeEntityType entityType, long entityId)
    {
        var result = new CrmSyncResult();
        var sw = Stopwatch.StartNew();

        try
        {
            var connection = await GetConnectionAsync(connectionId);
            if (connection == null || !connection.IsEnabled)
            {
                result.Errors.Add("Connection not found or disabled");
                result.Message = "Connection not found or disabled";
                return result;
            }

            // Find mappings for this entity type
            var mappings = await _context.CrmEntityMappings
                .Include(m => m.FieldMappings)
                .Include(m => m.RelationshipMappings)
                .Where(m => m.CrmConnectionId == connectionId &&
                           m.AuthScapeEntityType == entityType &&
                           m.IsEnabled &&
                           (m.SyncDirection == CrmSyncDirection.Outbound ||
                            m.SyncDirection == CrmSyncDirection.Bidirectional))
                .ToListAsync();

            foreach (var mapping in mappings)
            {
                var syncResult = await SyncSingleRecordOutboundAsync(connection, mapping, entityId);
                result.Stats.TotalProcessed++;
                result.Stats.OutboundCount++;
                result.Stats.CreatedCount += syncResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += syncResult.Stats.UpdatedCount;
                result.Stats.FailedCount += syncResult.Stats.FailedCount;
                result.Errors.AddRange(syncResult.Errors);
            }

            result.Success = !result.Errors.Any();
            result.Stats.SuccessCount = result.Stats.TotalProcessed - result.Stats.FailedCount;
            result.Message = result.Success ? "Outbound sync completed successfully" : "Outbound sync completed with errors";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during outbound sync for {EntityType} {EntityId}", entityType, entityId);
            result.Errors.Add(ex.Message);
            result.Message = ex.Message;
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async Task<CrmSyncResult> TriggerOutboundSyncAsync(AuthScapeEntityType entityType, long entityId)
    {
        var result = new CrmSyncResult();
        var sw = Stopwatch.StartNew();

        try
        {
            // Find all active connections with mappings for this entity type
            var connections = await _context.CrmConnections
                .Include(c => c.EntityMappings)
                    .ThenInclude(m => m.FieldMappings)
                .Include(c => c.EntityMappings)
                    .ThenInclude(m => m.RelationshipMappings)
                .Where(c => c.IsEnabled &&
                           c.EntityMappings.Any(m => m.AuthScapeEntityType == entityType &&
                                                    m.IsEnabled &&
                                                    (m.SyncDirection == CrmSyncDirection.Outbound ||
                                                     m.SyncDirection == CrmSyncDirection.Bidirectional)))
                .ToListAsync();

            if (!connections.Any())
            {
                _logger.LogDebug("No active CRM connections found for {EntityType}", entityType);
                result.Success = true;
                result.Message = "No CRM connections configured for this entity type";
                return result;
            }

            _logger.LogInformation("Triggering outbound sync for {EntityType} {EntityId} to {Count} connections",
                entityType, entityId, connections.Count);

            foreach (var connection in connections)
            {
                var syncResult = await SyncOutboundAsync(connection.Id, entityType, entityId);
                result.Stats.TotalProcessed += syncResult.Stats.TotalProcessed;
                result.Stats.CreatedCount += syncResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += syncResult.Stats.UpdatedCount;
                result.Stats.FailedCount += syncResult.Stats.FailedCount;
                result.Stats.OutboundCount += syncResult.Stats.OutboundCount;
                result.Errors.AddRange(syncResult.Errors);
            }

            result.Success = !result.Errors.Any();
            result.Stats.SuccessCount = result.Stats.TotalProcessed - result.Stats.FailedCount;
            result.Message = result.Success
                ? $"Synced to {connections.Count} CRM connection(s)"
                : $"Sync completed with {result.Errors.Count} error(s)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during triggered outbound sync for {EntityType} {EntityId}", entityType, entityId);
            result.Errors.Add(ex.Message);
            result.Message = ex.Message;
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async Task<CrmSyncResult> SyncInboundAsync(long connectionId, string crmEntityName, string crmRecordId)
    {
        var result = new CrmSyncResult();
        var sw = Stopwatch.StartNew();

        try
        {
            var connection = await GetConnectionAsync(connectionId);
            if (connection == null || !connection.IsEnabled)
            {
                result.Errors.Add("Connection not found or disabled");
                result.Message = "Connection not found or disabled";
                return result;
            }

            var mapping = await _context.CrmEntityMappings
                .Include(m => m.FieldMappings)
                .Include(m => m.RelationshipMappings)
                .FirstOrDefaultAsync(m => m.CrmConnectionId == connectionId &&
                                         m.CrmEntityName == crmEntityName &&
                                         m.IsEnabled &&
                                         (m.SyncDirection == CrmSyncDirection.Inbound ||
                                          m.SyncDirection == CrmSyncDirection.Bidirectional));

            if (mapping == null)
            {
                result.Errors.Add($"No mapping found for entity {crmEntityName}");
                result.Message = $"No mapping found for entity {crmEntityName}";
                return result;
            }

            var syncResult = await SyncSingleRecordInboundAsync(connection, mapping, crmRecordId);
            result.Stats.TotalProcessed++;
            result.Stats.InboundCount++;
            result.Stats.CreatedCount += syncResult.Stats.CreatedCount;
            result.Stats.UpdatedCount += syncResult.Stats.UpdatedCount;
            result.Stats.FailedCount += syncResult.Stats.FailedCount;
            result.Errors.AddRange(syncResult.Errors);

            result.Success = !result.Errors.Any();
            result.Stats.SuccessCount = result.Stats.TotalProcessed - result.Stats.FailedCount;
            result.Message = result.Success ? "Inbound sync completed successfully" : "Inbound sync completed with errors";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during inbound sync for {CrmEntityName} {CrmRecordId}", crmEntityName, crmRecordId);
            result.Errors.Add(ex.Message);
            result.Message = ex.Message;
        }

        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async Task<CrmSyncResult> ProcessWebhookAsync(long connectionId, CrmWebhookEvent webhookEvent)
    {
        _logger.LogInformation("Processing webhook for {EntityName} {RecordId} ({EventType})",
            webhookEvent.EntityName, webhookEvent.RecordId, webhookEvent.EventType);

        return await SyncInboundAsync(connectionId, webhookEvent.EntityName, webhookEvent.RecordId);
    }

    #endregion

    #region Sync Logs & External IDs

    public async Task<IEnumerable<CrmSyncLog>> GetSyncLogsAsync(long connectionId, int limit = 100, CrmSyncStatus? status = null, DateTimeOffset? since = null)
    {
        var query = _context.CrmSyncLogs.Where(l => l.CrmConnectionId == connectionId);

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        if (since.HasValue)
        {
            query = query.Where(l => l.SyncedAt >= since.Value);
        }

        return await query
            .OrderByDescending(l => l.SyncedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task ClearSyncLogsAsync(long connectionId, DateTimeOffset? olderThan = null)
    {
        var query = _context.CrmSyncLogs.Where(l => l.CrmConnectionId == connectionId);

        if (olderThan.HasValue)
        {
            query = query.Where(l => l.SyncedAt < olderThan.Value);
        }

        var logs = await query.ToListAsync();
        _context.CrmSyncLogs.RemoveRange(logs);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<CrmExternalId>> GetExternalIdsAsync(long connectionId, AuthScapeEntityType? entityType = null, int limit = 100)
    {
        var query = _context.CrmExternalIds.Where(e => e.CrmConnectionId == connectionId);

        if (entityType.HasValue)
        {
            query = query.Where(e => e.AuthScapeEntityType == entityType.Value);
        }

        return await query
            .OrderByDescending(e => e.LastSyncedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<CrmSyncStats> GetSyncStatsAsync(long connectionId, DateTimeOffset? since = null)
    {
        var query = _context.CrmSyncLogs.Where(l => l.CrmConnectionId == connectionId);

        if (since.HasValue)
        {
            query = query.Where(l => l.SyncedAt >= since.Value);
        }

        var logs = await query.ToListAsync();

        return new CrmSyncStats
        {
            TotalProcessed = logs.Count,
            SuccessCount = logs.Count(l => l.Status == CrmSyncStatus.Success),
            FailedCount = logs.Count(l => l.Status == CrmSyncStatus.Failed),
            ConflictCount = logs.Count(l => l.Status == CrmSyncStatus.Conflict),
            SkippedCount = logs.Count(l => l.Status == CrmSyncStatus.Skipped),
            InboundCount = logs.Count(l => l.Direction == "Inbound"),
            OutboundCount = logs.Count(l => l.Direction == "Outbound"),
            CreatedCount = logs.Count(l => l.Action == CrmSyncAction.Create && l.Status == CrmSyncStatus.Success),
            UpdatedCount = logs.Count(l => l.Action == CrmSyncAction.Update && l.Status == CrmSyncStatus.Success),
            DeletedCount = logs.Count(l => l.Action == CrmSyncAction.Delete && l.Status == CrmSyncStatus.Success),
            LastSyncAt = logs.Any() ? logs.Max(l => l.SyncedAt) : null,
            LastSuccessfulSyncAt = logs.Where(l => l.Status == CrmSyncStatus.Success).Any()
                ? logs.Where(l => l.Status == CrmSyncStatus.Success).Max(l => l.SyncedAt)
                : null
        };
    }

    public async Task<long?> GetAuthScapeEntityIdByCrmIdAsync(long connectionId, string crmEntityName, string crmEntityId)
    {
        var externalId = await _context.CrmExternalIds
            .Where(e => e.CrmConnectionId == connectionId &&
                       e.CrmEntityName == crmEntityName &&
                       e.CrmEntityId == crmEntityId)
            .Select(e => (long?)e.AuthScapeEntityId)
            .FirstOrDefaultAsync();

        return externalId;
    }

    public async Task<string?> GetCrmIdByAuthScapeEntityAsync(long connectionId, AuthScapeEntityType entityType, long entityId)
    {
        var crmId = await _context.CrmExternalIds
            .Where(e => e.CrmConnectionId == connectionId &&
                       e.AuthScapeEntityType == entityType &&
                       e.AuthScapeEntityId == entityId)
            .Select(e => e.CrmEntityId)
            .FirstOrDefaultAsync();

        return crmId;
    }

    public async Task<CrmExternalId?> GetExternalIdByCrmIdAsync(long connectionId, string crmEntityName, string crmEntityId)
    {
        return await _context.CrmExternalIds
            .FirstOrDefaultAsync(e => e.CrmConnectionId == connectionId &&
                                     e.CrmEntityName == crmEntityName &&
                                     e.CrmEntityId == crmEntityId);
    }

    public async Task<CrmExternalId?> GetExternalIdByAuthScapeEntityAsync(long connectionId, AuthScapeEntityType entityType, long entityId)
    {
        return await _context.CrmExternalIds
            .FirstOrDefaultAsync(e => e.CrmConnectionId == connectionId &&
                                     e.AuthScapeEntityType == entityType &&
                                     e.AuthScapeEntityId == entityId);
    }

    public async Task<CrmSyncDiagnostics> GetSyncDiagnosticsAsync(long connectionId)
    {
        var diagnostics = new CrmSyncDiagnostics();

        try
        {
            // Get connection info
            var connection = await GetConnectionAsync(connectionId);
            diagnostics.ConnectionStatus = connection == null ? "Not Found" :
                connection.IsEnabled ? "Enabled" : "Disabled";

            // Count total entities in AuthScape
            diagnostics.TotalUsers = await _context.Users.CountAsync();
            diagnostics.TotalLocations = await _context.Locations.CountAsync();
            diagnostics.TotalCompanies = await _context.Companies.CountAsync();

            // Count CrmExternalIds mappings
            diagnostics.UserToContactMappings = await _context.CrmExternalIds
                .CountAsync(e => e.CrmConnectionId == connectionId &&
                               e.AuthScapeEntityType == AuthScapeEntityType.User &&
                               e.CrmEntityName == "contact");

            diagnostics.LocationToAccountMappings = await _context.CrmExternalIds
                .CountAsync(e => e.CrmConnectionId == connectionId &&
                               e.AuthScapeEntityType == AuthScapeEntityType.Location &&
                               e.CrmEntityName == "account");

            diagnostics.CompanyToAccountMappings = await _context.CrmExternalIds
                .CountAsync(e => e.CrmConnectionId == connectionId &&
                               e.AuthScapeEntityType == AuthScapeEntityType.Company &&
                               e.CrmEntityName == "account");

            // Get sample User → Contact mappings
            var sampleUserMappings = await _context.CrmExternalIds
                .Where(e => e.CrmConnectionId == connectionId &&
                           e.AuthScapeEntityType == AuthScapeEntityType.User &&
                           e.CrmEntityName == "contact")
                .Take(5)
                .ToListAsync();

            foreach (var mapping in sampleUserMappings)
            {
                var user = await _context.Users.FindAsync(mapping.AuthScapeEntityId);
                diagnostics.SampleUserMappings.Add($"User {mapping.AuthScapeEntityId} ({user?.Email ?? "?"}) -> Contact {mapping.CrmEntityId}");
            }

            // Get sample Location/Company → Account mappings
            var sampleAccountMappings = await _context.CrmExternalIds
                .Where(e => e.CrmConnectionId == connectionId &&
                           (e.AuthScapeEntityType == AuthScapeEntityType.Location || e.AuthScapeEntityType == AuthScapeEntityType.Company) &&
                           e.CrmEntityName == "account")
                .Take(5)
                .ToListAsync();

            foreach (var mapping in sampleAccountMappings)
            {
                string entityName = mapping.AuthScapeEntityType == AuthScapeEntityType.Location ? "Location" : "Company";
                diagnostics.SampleAccountMappings.Add($"{entityName} {mapping.AuthScapeEntityId} -> Account {mapping.CrmEntityId}");
            }

            // Try to check a few contacts in CRM for bcbi_companyid
            if (connection != null && connection.IsEnabled && sampleUserMappings.Any())
            {
                try
                {
                    var provider = _providerFactory.GetProvider(connection);
                    var contactsWithCompanyId = 0;

                    foreach (var mapping in sampleUserMappings.Take(10))
                    {
                        var contact = await provider.GetRecordAsync(connection, "contact", mapping.CrmEntityId);
                        if (contact != null)
                        {
                            // Check for bcbi_companyid or parentcustomerid
                            var hasCompanyId = contact.Fields.Any(f =>
                                (f.Key.Contains("bcbi_companyid") || f.Key.Contains("parentcustomerid")) &&
                                f.Value != null && !string.IsNullOrEmpty(f.Value.ToString()));
                            if (hasCompanyId)
                            {
                                contactsWithCompanyId++;
                            }
                        }
                    }

                    diagnostics.ContactsWithCompanyId = contactsWithCompanyId;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking CRM contacts for bcbi_companyid");
                }
            }

            // Generate recommendation
            if (diagnostics.UserToContactMappings == 0)
            {
                diagnostics.Recommendation = "No User → Contact mappings found. You need to sync Users to CRM Contacts first (or sync Contacts from CRM to Users). Create an Entity Mapping: User → contact with Inbound or Bidirectional direction, then run a sync.";
            }
            else if (diagnostics.LocationToAccountMappings == 0 && diagnostics.CompanyToAccountMappings == 0)
            {
                diagnostics.Recommendation = "No Location/Company → Account mappings found. You need to sync Locations or Companies to CRM Accounts first. Create an Entity Mapping: Location → account with Inbound or Bidirectional direction, then run a sync.";
            }
            else if (diagnostics.ContactsWithCompanyId == 0)
            {
                diagnostics.Recommendation = "CRM Contacts don't appear to have bcbi_companyid or parentcustomerid set. Check if the CRM contacts have a company assigned.";
            }
            else
            {
                diagnostics.Recommendation = "Mappings look good! Try running Sync User Locations from CRM.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync diagnostics");
            diagnostics.Recommendation = $"Error getting diagnostics: {ex.Message}";
        }

        return diagnostics;
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Counts the total records that will be synced for progress tracking
    /// </summary>
    private async Task<int> CountRecordsForSyncAsync(CrmConnection connection, CrmEntityMapping mapping, bool isFullSync)
    {
        var totalCount = 0;

        try
        {
            // Count outbound records if configured
            if (mapping.SyncDirection == CrmSyncDirection.Outbound ||
                mapping.SyncDirection == CrmSyncDirection.Bidirectional)
            {
                totalCount += await CountAuthScapeEntitiesAsync(mapping.AuthScapeEntityType);
            }

            // Count inbound records if configured
            if (mapping.SyncDirection == CrmSyncDirection.Inbound ||
                mapping.SyncDirection == CrmSyncDirection.Bidirectional)
            {
                var provider = _providerFactory.GetProvider(connection);
                DateTimeOffset? modifiedSince = isFullSync ? null : connection.LastSyncAt;
                var records = await provider.GetRecordsAsync(connection, mapping.CrmEntityName, modifiedSince, mapping.CrmFilterExpression);
                totalCount += records.Count();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error counting records for sync progress");
        }

        return totalCount;
    }

    /// <summary>
    /// Counts AuthScape entities of a given type
    /// </summary>
    private async Task<int> CountAuthScapeEntitiesAsync(AuthScapeEntityType entityType)
    {
        return entityType switch
        {
            AuthScapeEntityType.User => await _context.Users.CountAsync(),
            AuthScapeEntityType.Company => await _context.Companies.CountAsync(),
            AuthScapeEntityType.Location => await _context.Locations.CountAsync(),
            _ => 0
        };
    }

    /// <summary>
    /// Syncs an entity mapping with progress reporting
    /// </summary>
    private async Task<CrmSyncResult> SyncEntityMappingWithProgressAsync(CrmConnection connection, CrmEntityMapping mapping, bool isFullSync, string? syncId)
    {
        var result = new CrmSyncResult();
        var processedCount = 0;

        try
        {
            var provider = _providerFactory.GetProvider(connection);

            // Sync outbound if configured
            if (mapping.SyncDirection == CrmSyncDirection.Outbound ||
                mapping.SyncDirection == CrmSyncDirection.Bidirectional)
            {
                var entityIds = await GetAuthScapeEntityIdsAsync(mapping.AuthScapeEntityType);
                var entityIdList = entityIds.ToList();

                foreach (var entityId in entityIdList)
                {
                    var singleResult = await SyncSingleRecordOutboundAsync(connection, mapping, entityId);
                    result.Stats.TotalProcessed++;
                    result.Stats.CreatedCount += singleResult.Stats.CreatedCount;
                    result.Stats.UpdatedCount += singleResult.Stats.UpdatedCount;
                    result.Stats.FailedCount += singleResult.Stats.FailedCount;
                    result.Stats.OutboundCount++;
                    result.Errors.AddRange(singleResult.Errors);

                    processedCount++;
                    if (syncId != null && _progressService != null)
                    {
                        await _progressService.ReportProgressAsync(syncId, processedCount, $"Syncing {mapping.AuthScapeEntityType} {entityId} to CRM");
                    }
                }
            }

            // Sync inbound if configured
            if (mapping.SyncDirection == CrmSyncDirection.Inbound ||
                mapping.SyncDirection == CrmSyncDirection.Bidirectional)
            {
                DateTimeOffset? modifiedSince = isFullSync ? null : connection.LastSyncAt;
                var crmRecords = await provider.GetRecordsAsync(connection, mapping.CrmEntityName, modifiedSince, mapping.CrmFilterExpression);
                var recordList = crmRecords.ToList();

                foreach (var crmRecord in recordList)
                {
                    var singleResult = await SyncSingleRecordInboundAsync(connection, mapping, crmRecord.Id);
                    result.Stats.TotalProcessed++;
                    result.Stats.CreatedCount += singleResult.Stats.CreatedCount;
                    result.Stats.UpdatedCount += singleResult.Stats.UpdatedCount;
                    result.Stats.FailedCount += singleResult.Stats.FailedCount;
                    result.Stats.InboundCount++;
                    result.Errors.AddRange(singleResult.Errors);

                    processedCount++;
                    if (syncId != null && _progressService != null)
                    {
                        await _progressService.ReportProgressAsync(syncId, processedCount, $"Syncing {crmRecord.Id} from CRM");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing entity mapping {MappingId}", mapping.Id);
            result.Errors.Add($"Error syncing {mapping.CrmEntityName}: {ex.Message}");
        }

        return result;
    }

    private async Task<CrmSyncResult> SyncEntityMappingAsync(CrmConnection connection, CrmEntityMapping mapping, bool isFullSync = false)
    {
        var result = new CrmSyncResult();

        try
        {
            var provider = _providerFactory.GetProvider(connection);

            // Sync outbound if configured
            if (mapping.SyncDirection == CrmSyncDirection.Outbound ||
                mapping.SyncDirection == CrmSyncDirection.Bidirectional)
            {
                var outboundResult = await SyncOutboundForMappingAsync(connection, mapping, provider);
                result.Stats.TotalProcessed += outboundResult.Stats.TotalProcessed;
                result.Stats.CreatedCount += outboundResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += outboundResult.Stats.UpdatedCount;
                result.Stats.FailedCount += outboundResult.Stats.FailedCount;
                result.Stats.OutboundCount += outboundResult.Stats.TotalProcessed;
                result.Errors.AddRange(outboundResult.Errors);
            }

            // Sync inbound if configured
            if (mapping.SyncDirection == CrmSyncDirection.Inbound ||
                mapping.SyncDirection == CrmSyncDirection.Bidirectional)
            {
                var inboundResult = await SyncInboundForMappingAsync(connection, mapping, provider, isFullSync);
                result.Stats.TotalProcessed += inboundResult.Stats.TotalProcessed;
                result.Stats.CreatedCount += inboundResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += inboundResult.Stats.UpdatedCount;
                result.Stats.FailedCount += inboundResult.Stats.FailedCount;
                result.Stats.InboundCount += inboundResult.Stats.TotalProcessed;
                result.Errors.AddRange(inboundResult.Errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing entity mapping {MappingId}", mapping.Id);
            result.Errors.Add($"Error syncing {mapping.CrmEntityName}: {ex.Message}");
        }

        return result;
    }

    private async Task<CrmSyncResult> SyncEntityMappingWithProgressAsync(CrmConnection connection, CrmEntityMapping mapping, bool isFullSync, Action<string, int, int>? progressCallback)
    {
        var result = new CrmSyncResult();
        var entityName = mapping.CrmEntityDisplayName ?? mapping.CrmEntityName ?? $"Mapping {mapping.Id}";

        try
        {
            var provider = _providerFactory.GetProvider(connection);

            // Sync outbound if configured
            if (mapping.SyncDirection == CrmSyncDirection.Outbound ||
                mapping.SyncDirection == CrmSyncDirection.Bidirectional)
            {
                var outboundResult = await SyncOutboundForMappingWithProgressAsync(connection, mapping, provider, entityName, progressCallback);
                result.Stats.TotalProcessed += outboundResult.Stats.TotalProcessed;
                result.Stats.CreatedCount += outboundResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += outboundResult.Stats.UpdatedCount;
                result.Stats.FailedCount += outboundResult.Stats.FailedCount;
                result.Stats.OutboundCount += outboundResult.Stats.TotalProcessed;
                result.Errors.AddRange(outboundResult.Errors);
            }

            // Sync inbound if configured
            if (mapping.SyncDirection == CrmSyncDirection.Inbound ||
                mapping.SyncDirection == CrmSyncDirection.Bidirectional)
            {
                var inboundResult = await SyncInboundForMappingWithProgressAsync(connection, mapping, provider, isFullSync, entityName, progressCallback);
                result.Stats.TotalProcessed += inboundResult.Stats.TotalProcessed;
                result.Stats.CreatedCount += inboundResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += inboundResult.Stats.UpdatedCount;
                result.Stats.FailedCount += inboundResult.Stats.FailedCount;
                result.Stats.InboundCount += inboundResult.Stats.TotalProcessed;
                result.Errors.AddRange(inboundResult.Errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing entity mapping {MappingId}", mapping.Id);
            result.Errors.Add($"Error syncing {mapping.CrmEntityName}: {ex.Message}");
        }

        return result;
    }

    private async Task<CrmSyncResult> SyncOutboundForMappingWithProgressAsync(CrmConnection connection, CrmEntityMapping mapping, ICrmProvider provider, string entityName, Action<string, int, int>? progressCallback)
    {
        var result = new CrmSyncResult();

        try
        {
            // Get AuthScape records based on entity type
            var entityIds = (await GetAuthScapeEntityIdsAsync(mapping.AuthScapeEntityType)).ToList();
            var total = entityIds.Count;
            var current = 0;

            foreach (var entityId in entityIds)
            {
                current++;
                progressCallback?.Invoke($"{entityName} (Outbound)", current, total);

                var singleResult = await SyncSingleRecordOutboundAsync(connection, mapping, entityId);
                result.Stats.TotalProcessed++;
                result.Stats.CreatedCount += singleResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += singleResult.Stats.UpdatedCount;
                result.Stats.FailedCount += singleResult.Stats.FailedCount;
                result.Errors.AddRange(singleResult.Errors);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Outbound sync error: {ex.Message}");
        }

        return result;
    }

    private async Task<CrmSyncResult> SyncInboundForMappingWithProgressAsync(CrmConnection connection, CrmEntityMapping mapping, ICrmProvider provider, bool isFullSync, string entityName, Action<string, int, int>? progressCallback)
    {
        var result = new CrmSyncResult();

        try
        {
            // For full sync, pass null to get ALL records; for incremental, use LastSyncAt
            DateTimeOffset? modifiedSince = isFullSync ? null : connection.LastSyncAt;

            _logger.LogInformation("Syncing inbound for {EntityName}: isFullSync={IsFullSync}, modifiedSince={ModifiedSince}",
                mapping.CrmEntityName, isFullSync, modifiedSince);

            // Get CRM records
            var crmRecords = await provider.GetRecordsAsync(
                connection,
                mapping.CrmEntityName,
                modifiedSince,
                mapping.CrmFilterExpression);

            var recordList = crmRecords.ToList();
            var total = recordList.Count;
            var current = 0;

            _logger.LogInformation("Found {Count} CRM records to sync inbound for {EntityName}",
                recordList.Count, mapping.CrmEntityName);

            foreach (var crmRecord in recordList)
            {
                current++;
                progressCallback?.Invoke($"{entityName} (Inbound)", current, total);

                var singleResult = await SyncSingleRecordInboundAsync(connection, mapping, crmRecord.Id);
                result.Stats.TotalProcessed++;
                result.Stats.CreatedCount += singleResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += singleResult.Stats.UpdatedCount;
                result.Stats.FailedCount += singleResult.Stats.FailedCount;
                result.Errors.AddRange(singleResult.Errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inbound sync error for {EntityName}", mapping.CrmEntityName);
            result.Errors.Add($"Inbound sync error: {ex.Message}");
        }

        return result;
    }

    private async Task<CrmSyncResult> SyncOutboundForMappingAsync(CrmConnection connection, CrmEntityMapping mapping, ICrmProvider provider)
    {
        var result = new CrmSyncResult();

        try
        {
            // Get AuthScape records based on entity type
            var entityIds = await GetAuthScapeEntityIdsAsync(mapping.AuthScapeEntityType);

            foreach (var entityId in entityIds)
            {
                var singleResult = await SyncSingleRecordOutboundAsync(connection, mapping, entityId);
                result.Stats.TotalProcessed++;
                result.Stats.CreatedCount += singleResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += singleResult.Stats.UpdatedCount;
                result.Stats.FailedCount += singleResult.Stats.FailedCount;
                result.Errors.AddRange(singleResult.Errors);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Outbound sync error: {ex.Message}");
        }

        return result;
    }

    private async Task<CrmSyncResult> SyncInboundForMappingAsync(CrmConnection connection, CrmEntityMapping mapping, ICrmProvider provider, bool isFullSync = false)
    {
        var result = new CrmSyncResult();

        try
        {
            // For full sync, pass null to get ALL records; for incremental, use LastSyncAt
            DateTimeOffset? modifiedSince = isFullSync ? null : connection.LastSyncAt;

            _logger.LogInformation("Syncing inbound for {EntityName}: isFullSync={IsFullSync}, modifiedSince={ModifiedSince}",
                mapping.CrmEntityName, isFullSync, modifiedSince);

            // Get CRM records
            var crmRecords = await provider.GetRecordsAsync(
                connection,
                mapping.CrmEntityName,
                modifiedSince,
                mapping.CrmFilterExpression);

            var recordList = crmRecords.ToList();
            _logger.LogInformation("Found {Count} CRM records to sync inbound for {EntityName}",
                recordList.Count, mapping.CrmEntityName);

            foreach (var crmRecord in recordList)
            {
                var singleResult = await SyncSingleRecordInboundAsync(connection, mapping, crmRecord.Id);
                result.Stats.TotalProcessed++;
                result.Stats.CreatedCount += singleResult.Stats.CreatedCount;
                result.Stats.UpdatedCount += singleResult.Stats.UpdatedCount;
                result.Stats.FailedCount += singleResult.Stats.FailedCount;
                result.Errors.AddRange(singleResult.Errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inbound sync error for {EntityName}", mapping.CrmEntityName);
            result.Errors.Add($"Inbound sync error: {ex.Message}");
        }

        return result;
    }

    private async Task<CrmSyncResult> SyncSingleRecordOutboundAsync(CrmConnection connection, CrmEntityMapping mapping, long entityId)
    {
        var result = new CrmSyncResult();
        var sw = Stopwatch.StartNew();

        try
        {
            var provider = _providerFactory.GetProvider(connection);

            // Get AuthScape entity data
            var entityData = await GetAuthScapeEntityDataAsync(mapping.AuthScapeEntityType, entityId);
            if (entityData == null)
            {
                result.Stats.SkippedCount++;
                return result;
            }

            // Map fields to CRM format
            var crmFields = MapAuthScapeToCrm(entityData, mapping.FieldMappings.Where(f => f.IsEnabled));

            // Resolve relationship fields (e.g., CompanyId → parentcustomerid lookup)
            await ResolveOutboundRelationshipsAsync(connection, mapping, entityData, crmFields);

            // Check if we have an existing external ID mapping
            var externalId = await _context.CrmExternalIds
                .FirstOrDefaultAsync(e => e.CrmConnectionId == connection.Id &&
                                         e.AuthScapeEntityType == mapping.AuthScapeEntityType &&
                                         e.AuthScapeEntityId == entityId);

            if (externalId != null)
            {
                // Update existing CRM record via external ID mapping
                await provider.UpdateRecordAsync(connection, mapping.CrmEntityName, externalId.CrmEntityId, crmFields);
                externalId.LastSyncedAt = DateTimeOffset.UtcNow;
                externalId.LastSyncDirection = "Outbound";
                result.Stats.UpdatedCount++;
                await LogSync(connection.Id, mapping.Id, mapping.AuthScapeEntityType, entityId,
                    mapping.CrmEntityName, externalId.CrmEntityId, "Outbound", CrmSyncAction.Update,
                    CrmSyncStatus.Success, sw.ElapsedMilliseconds);
            }
            else
            {
                // No external ID mapping - try to find existing CRM record by identifier (e.g., email)
                var existingCrmRecord = await FindExistingCrmRecordByIdentifierAsync(provider, connection, mapping, entityData);

                if (existingCrmRecord != null)
                {
                    // Found existing CRM record by identifier - update it and create mapping
                    await provider.UpdateRecordAsync(connection, mapping.CrmEntityName, existingCrmRecord.Id, crmFields);

                    // Create external ID mapping for future syncs
                    var newExternalId = new CrmExternalId
                    {
                        CrmConnectionId = connection.Id,
                        AuthScapeEntityType = mapping.AuthScapeEntityType,
                        AuthScapeEntityId = entityId,
                        CrmEntityName = mapping.CrmEntityName,
                        CrmEntityId = existingCrmRecord.Id,
                        LastSyncedAt = DateTimeOffset.UtcNow,
                        LastSyncDirection = "Outbound"
                    };
                    _context.CrmExternalIds.Add(newExternalId);

                    result.Stats.UpdatedCount++;
                    await LogSync(connection.Id, mapping.Id, mapping.AuthScapeEntityType, entityId,
                        mapping.CrmEntityName, existingCrmRecord.Id, "Outbound", CrmSyncAction.Update,
                        CrmSyncStatus.Success, sw.ElapsedMilliseconds);

                    _logger.LogInformation("Linked AuthScape {EntityType} {EntityId} to existing CRM {CrmEntity} {CrmId} by identifier match",
                        mapping.AuthScapeEntityType, entityId, mapping.CrmEntityName, existingCrmRecord.Id);
                }
                else
                {
                    // No existing CRM record found - create new CRM record
                    var crmRecordId = await provider.CreateRecordAsync(connection, mapping.CrmEntityName, crmFields);

                    // Store external ID mapping
                    var newExternalId = new CrmExternalId
                    {
                        CrmConnectionId = connection.Id,
                        AuthScapeEntityType = mapping.AuthScapeEntityType,
                        AuthScapeEntityId = entityId,
                        CrmEntityName = mapping.CrmEntityName,
                        CrmEntityId = crmRecordId,
                        LastSyncedAt = DateTimeOffset.UtcNow,
                        LastSyncDirection = "Outbound"
                    };
                    _context.CrmExternalIds.Add(newExternalId);

                    result.Stats.CreatedCount++;
                    await LogSync(connection.Id, mapping.Id, mapping.AuthScapeEntityType, entityId,
                        mapping.CrmEntityName, crmRecordId, "Outbound", CrmSyncAction.Create,
                        CrmSyncStatus.Success, sw.ElapsedMilliseconds);
                }
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            result.Stats.FailedCount++;
            result.Errors.Add($"Outbound sync error: {ex.Message}");
            await LogSync(connection.Id, mapping.Id, mapping.AuthScapeEntityType, entityId,
                mapping.CrmEntityName, null, "Outbound", CrmSyncAction.Update,
                CrmSyncStatus.Failed, sw.ElapsedMilliseconds, ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Finds an existing CRM record by a natural identifier (email for contacts, name for accounts, etc.)
    /// This enables matching AuthScape entities to existing CRM records without a prior external ID mapping.
    /// </summary>
    private async Task<CrmRecord?> FindExistingCrmRecordByIdentifierAsync(
        ICrmProvider provider,
        CrmConnection connection,
        CrmEntityMapping mapping,
        Dictionary<string, object?> entityData)
    {
        string? filterExpression = null;

        switch (mapping.AuthScapeEntityType)
        {
            case AuthScapeEntityType.User:
                // Match by email address in CRM
                var email = entityData.GetValueOrDefault("Email")?.ToString();
                if (!string.IsNullOrEmpty(email))
                {
                    // Dynamics uses emailaddress1 for contacts
                    filterExpression = $"emailaddress1 eq '{email}'";
                }
                break;

            case AuthScapeEntityType.Company:
                // Match by company name in CRM
                var companyTitle = entityData.GetValueOrDefault("Title")?.ToString();
                if (!string.IsNullOrEmpty(companyTitle))
                {
                    // Dynamics uses 'name' for accounts
                    filterExpression = $"name eq '{companyTitle.Replace("'", "''")}'";
                }
                break;

            case AuthScapeEntityType.Location:
                // Match by location name in CRM
                var locationTitle = entityData.GetValueOrDefault("Title")?.ToString();
                if (!string.IsNullOrEmpty(locationTitle))
                {
                    filterExpression = $"name eq '{locationTitle.Replace("'", "''")}'";
                }
                break;
        }

        if (string.IsNullOrEmpty(filterExpression))
            return null;

        try
        {
            var records = await provider.GetRecordsAsync(connection, mapping.CrmEntityName, filter: filterExpression, top: 1);
            return records.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching for existing CRM record with filter: {Filter}", filterExpression);
            return null;
        }
    }

    private async Task<CrmSyncResult> SyncSingleRecordInboundAsync(CrmConnection connection, CrmEntityMapping mapping, string crmRecordId)
    {
        var result = new CrmSyncResult();
        var sw = Stopwatch.StartNew();

        try
        {
            var provider = _providerFactory.GetProvider(connection);

            // Get CRM record data
            var crmRecord = await provider.GetRecordAsync(connection, mapping.CrmEntityName, crmRecordId);
            if (crmRecord == null)
            {
                result.Stats.SkippedCount++;
                return result;
            }

            // Map fields from CRM to AuthScape format
            var authScapeFields = MapCrmToAuthScape(crmRecord, mapping.FieldMappings.Where(f => f.IsEnabled));

            // Resolve relationship fields (e.g., parentcustomerid → CompanyId)
            await ResolveInboundRelationshipsAsync(connection, mapping, crmRecord, authScapeFields);

            // For users, auto-populate common fields from CRM if not already mapped
            if (mapping.AuthScapeEntityType == AuthScapeEntityType.User)
            {
                // Auto-populate Email from emailaddress1
                if (!authScapeFields.ContainsKey("Email") &&
                    crmRecord.Fields.TryGetValue("emailaddress1", out var crmEmailValue) &&
                    crmEmailValue != null)
                {
                    var crmEmailStr = crmEmailValue.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(crmEmailStr))
                    {
                        authScapeFields["Email"] = crmEmailStr;
                        _logger.LogDebug("Added Email from CRM emailaddress1 for record {CrmRecordId}: {Email}", crmRecordId, crmEmailStr);
                    }
                }

                // Auto-populate FirstName from firstname
                if (!authScapeFields.ContainsKey("FirstName") &&
                    crmRecord.Fields.TryGetValue("firstname", out var crmFirstName) &&
                    crmFirstName != null)
                {
                    var firstName = crmFirstName.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(firstName))
                    {
                        authScapeFields["FirstName"] = firstName;
                        _logger.LogDebug("Added FirstName from CRM firstname for record {CrmRecordId}: {FirstName}", crmRecordId, firstName);
                    }
                }

                // Auto-populate LastName from lastname
                if (!authScapeFields.ContainsKey("LastName") &&
                    crmRecord.Fields.TryGetValue("lastname", out var crmLastName) &&
                    crmLastName != null)
                {
                    var lastName = crmLastName.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(lastName))
                    {
                        authScapeFields["LastName"] = lastName;
                        _logger.LogDebug("Added LastName from CRM lastname for record {CrmRecordId}: {LastName}", crmRecordId, lastName);
                    }
                }

                // Auto-populate PhoneNumber from telephone1
                if (!authScapeFields.ContainsKey("PhoneNumber") &&
                    crmRecord.Fields.TryGetValue("telephone1", out var crmPhone) &&
                    crmPhone != null)
                {
                    var phone = crmPhone.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(phone))
                    {
                        authScapeFields["PhoneNumber"] = phone;
                    }
                }

                // Auto-populate IsActive from statecode (Dynamics: 0=Active, 1=Inactive)
                if (!authScapeFields.ContainsKey("IsActive") &&
                    crmRecord.Fields.TryGetValue("statecode", out var stateCode))
                {
                    // statecode 0 = Active (IsActive = true), statecode 1 = Inactive (IsActive = false)
                    bool isActive = true; // Default to active
                    if (stateCode != null)
                    {
                        var stateCodeStr = stateCode.ToString();
                        if (int.TryParse(stateCodeStr, out var stateCodeInt))
                        {
                            isActive = stateCodeInt == 0; // 0 = Active
                        }
                    }
                    authScapeFields["IsActive"] = isActive;
                    _logger.LogDebug("Set IsActive={IsActive} from CRM statecode for record {CrmRecordId}", isActive, crmRecordId);
                }
            }

            // For locations, auto-populate common fields from CRM account if not already mapped
            if (mapping.AuthScapeEntityType == AuthScapeEntityType.Location)
            {
                // Auto-populate Title from 'name' (Dynamics 365 account name field)
                if (!authScapeFields.ContainsKey("Title") &&
                    crmRecord.Fields.TryGetValue("name", out var crmName) &&
                    crmName != null)
                {
                    var nameStr = crmName.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(nameStr))
                    {
                        authScapeFields["Title"] = nameStr;
                        _logger.LogDebug("Added Title from CRM name for record {CrmRecordId}: {Title}", crmRecordId, nameStr);
                    }
                }

                // Auto-populate Address from address1_line1
                if (!authScapeFields.ContainsKey("Address") &&
                    crmRecord.Fields.TryGetValue("address1_line1", out var crmAddress) &&
                    crmAddress != null)
                {
                    var addressStr = crmAddress.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(addressStr))
                    {
                        authScapeFields["Address"] = addressStr;
                    }
                }

                // Auto-populate City from address1_city
                if (!authScapeFields.ContainsKey("City") &&
                    crmRecord.Fields.TryGetValue("address1_city", out var crmCity) &&
                    crmCity != null)
                {
                    var cityStr = crmCity.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(cityStr))
                    {
                        authScapeFields["City"] = cityStr;
                    }
                }

                // Auto-populate State from address1_stateorprovince
                if (!authScapeFields.ContainsKey("State") &&
                    crmRecord.Fields.TryGetValue("address1_stateorprovince", out var crmState) &&
                    crmState != null)
                {
                    var stateStr = crmState.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(stateStr))
                    {
                        authScapeFields["State"] = stateStr;
                    }
                }

                // Auto-populate ZipCode from address1_postalcode
                if (!authScapeFields.ContainsKey("ZipCode") &&
                    crmRecord.Fields.TryGetValue("address1_postalcode", out var crmZip) &&
                    crmZip != null)
                {
                    var zipStr = crmZip.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(zipStr))
                    {
                        authScapeFields["ZipCode"] = zipStr;
                    }
                }
            }

            // For companies, auto-populate common fields from CRM account if not already mapped
            if (mapping.AuthScapeEntityType == AuthScapeEntityType.Company)
            {
                // Auto-populate Title from 'name' (Dynamics 365 account name field)
                if (!authScapeFields.ContainsKey("Title") &&
                    crmRecord.Fields.TryGetValue("name", out var crmCompanyName) &&
                    crmCompanyName != null)
                {
                    var nameStr = crmCompanyName.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(nameStr))
                    {
                        authScapeFields["Title"] = nameStr;
                        _logger.LogDebug("Added Title from CRM name for record {CrmRecordId}: {Title}", crmRecordId, nameStr);
                    }
                }

                // Auto-populate Description from description
                if (!authScapeFields.ContainsKey("Description") &&
                    crmRecord.Fields.TryGetValue("description", out var crmDesc) &&
                    crmDesc != null)
                {
                    var descStr = crmDesc.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(descStr))
                    {
                        authScapeFields["Description"] = descStr;
                    }
                }
            }

            // Check if we have an existing external ID mapping
            var externalId = await _context.CrmExternalIds
                .FirstOrDefaultAsync(e => e.CrmConnectionId == connection.Id &&
                                         e.CrmEntityName == mapping.CrmEntityName &&
                                         e.CrmEntityId == crmRecordId);

            if (externalId != null)
            {
                // Update existing AuthScape record via external ID mapping
                await UpdateAuthScapeEntityAsync(mapping.AuthScapeEntityType, externalId.AuthScapeEntityId, authScapeFields);
                externalId.LastSyncedAt = DateTimeOffset.UtcNow;
                externalId.LastSyncDirection = "Inbound";
                result.Stats.UpdatedCount++;
                await LogSync(connection.Id, mapping.Id, mapping.AuthScapeEntityType, externalId.AuthScapeEntityId,
                    mapping.CrmEntityName, crmRecordId, "Inbound", CrmSyncAction.Update,
                    CrmSyncStatus.Success, sw.ElapsedMilliseconds);
            }
            else
            {
                // No external ID mapping - try to find existing AuthScape entity by identifier (e.g., email)
                var existingEntityId = await FindExistingAuthScapeEntityByIdentifierAsync(mapping.AuthScapeEntityType, authScapeFields, crmRecord);

                if (existingEntityId.HasValue)
                {
                    // Found existing entity by identifier (e.g., email) - update it
                    await UpdateAuthScapeEntityAsync(mapping.AuthScapeEntityType, existingEntityId.Value, authScapeFields);

                    // Check if this AuthScape entity already has an external ID mapping for this connection
                    var existingExternalId = await _context.CrmExternalIds
                        .FirstOrDefaultAsync(e => e.CrmConnectionId == connection.Id &&
                                                  e.AuthScapeEntityType == mapping.AuthScapeEntityType &&
                                                  e.AuthScapeEntityId == existingEntityId.Value);

                    if (existingExternalId != null)
                    {
                        // Update existing mapping (entity was matched by email, update the CRM record ID if different)
                        if (existingExternalId.CrmEntityId != crmRecordId)
                        {
                            _logger.LogWarning("AuthScape {EntityType} {EntityId} was linked to CRM {OldCrmId} but matched new CRM record {NewCrmId} by identifier - keeping original mapping",
                                mapping.AuthScapeEntityType, existingEntityId.Value, existingExternalId.CrmEntityId, crmRecordId);
                            // Skip this record - entity is already mapped to a different CRM record
                            result.Stats.SkippedCount++;
                            return result;
                        }
                        existingExternalId.LastSyncedAt = DateTimeOffset.UtcNow;
                        existingExternalId.LastSyncDirection = "Inbound";
                    }
                    else
                    {
                        // Create external ID mapping for future syncs
                        var newExternalId = new CrmExternalId
                        {
                            CrmConnectionId = connection.Id,
                            AuthScapeEntityType = mapping.AuthScapeEntityType,
                            AuthScapeEntityId = existingEntityId.Value,
                            CrmEntityName = mapping.CrmEntityName,
                            CrmEntityId = crmRecordId,
                            LastSyncedAt = DateTimeOffset.UtcNow,
                            LastSyncDirection = "Inbound"
                        };
                        _context.CrmExternalIds.Add(newExternalId);
                    }

                    result.Stats.UpdatedCount++;
                    await LogSync(connection.Id, mapping.Id, mapping.AuthScapeEntityType, existingEntityId.Value,
                        mapping.CrmEntityName, crmRecordId, "Inbound", CrmSyncAction.Update,
                        CrmSyncStatus.Success, sw.ElapsedMilliseconds);

                    _logger.LogInformation("Linked existing AuthScape {EntityType} {EntityId} to CRM {CrmEntity} {CrmId} by identifier match",
                        mapping.AuthScapeEntityType, existingEntityId.Value, mapping.CrmEntityName, crmRecordId);
                }
                else
                {
                    // No existing entity found - create new AuthScape record
                    var authScapeEntityId = await CreateAuthScapeEntityAsync(mapping.AuthScapeEntityType, authScapeFields);

                    // Store external ID mapping
                    var newExternalId = new CrmExternalId
                    {
                        CrmConnectionId = connection.Id,
                        AuthScapeEntityType = mapping.AuthScapeEntityType,
                        AuthScapeEntityId = authScapeEntityId,
                        CrmEntityName = mapping.CrmEntityName,
                        CrmEntityId = crmRecordId,
                        LastSyncedAt = DateTimeOffset.UtcNow,
                        LastSyncDirection = "Inbound"
                    };
                    _context.CrmExternalIds.Add(newExternalId);

                    result.Stats.CreatedCount++;
                    await LogSync(connection.Id, mapping.Id, mapping.AuthScapeEntityType, authScapeEntityId,
                        mapping.CrmEntityName, crmRecordId, "Inbound", CrmSyncAction.Create,
                        CrmSyncStatus.Success, sw.ElapsedMilliseconds);
                }
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            result.Stats.FailedCount++;
            // Include inner exception for better debugging
            var errorMessage = ex.InnerException != null
                ? $"Inbound sync error for CRM record {crmRecordId}: {ex.Message} -> {ex.InnerException.Message}"
                : $"Inbound sync error for CRM record {crmRecordId}: {ex.Message}";
            result.Errors.Add(errorMessage);
            _logger.LogError(ex, "Inbound sync error for CRM record {CrmRecordId}", crmRecordId);
            await LogSync(connection.Id, mapping.Id, mapping.AuthScapeEntityType, null,
                mapping.CrmEntityName, crmRecordId, "Inbound", CrmSyncAction.Update,
                CrmSyncStatus.Failed, sw.ElapsedMilliseconds, errorMessage);
        }

        return result;
    }

    /// <summary>
    /// Finds an existing AuthScape entity by a natural identifier (email for users, title for companies, etc.)
    /// This enables matching CRM records to existing AuthScape entities without a prior external ID mapping.
    /// </summary>
    private async Task<long?> FindExistingAuthScapeEntityByIdentifierAsync(
        AuthScapeEntityType entityType,
        Dictionary<string, object?> authScapeFields,
        CrmRecord crmRecord)
    {
        switch (entityType)
        {
            case AuthScapeEntityType.User:
                // First try emailaddress1 directly from CRM record (this is always available for contacts)
                if (crmRecord.Fields.TryGetValue("emailaddress1", out var crmEmail) && crmEmail != null)
                {
                    var crmEmailStr = crmEmail.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(crmEmailStr))
                    {
                        var existingUser = await _context.Users
                            .Where(u => u.Email != null && u.Email.ToLower() == crmEmailStr.ToLower())
                            .Select(u => (long?)u.Id)
                            .FirstOrDefaultAsync();
                        if (existingUser.HasValue)
                        {
                            _logger.LogDebug("Found existing user by CRM emailaddress1: {Email} -> UserId {UserId}", crmEmailStr, existingUser.Value);
                            return existingUser;
                        }
                    }
                }

                // Then try the mapped Email field
                var email = authScapeFields.GetValueOrDefault("Email")?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(email))
                {
                    var existingUser = await _context.Users
                        .Where(u => u.Email != null && u.Email.ToLower() == email.ToLower())
                        .Select(u => (long?)u.Id)
                        .FirstOrDefaultAsync();
                    if (existingUser.HasValue)
                    {
                        _logger.LogDebug("Found existing user by mapped Email: {Email} -> UserId {UserId}", email, existingUser.Value);
                        return existingUser;
                    }
                }
                break;

            case AuthScapeEntityType.Company:
                // Match by company title/name
                var companyTitle = authScapeFields.GetValueOrDefault("Title")?.ToString();
                if (!string.IsNullOrEmpty(companyTitle))
                {
                    var existingCompany = await _context.Companies
                        .Where(c => c.Title != null && c.Title.ToLower() == companyTitle.ToLower())
                        .Select(c => (long?)c.Id)
                        .FirstOrDefaultAsync();
                    if (existingCompany.HasValue)
                        return existingCompany;
                }
                // Also try matching by 'name' field from CRM (common for accounts)
                if (crmRecord.Fields.TryGetValue("name", out var crmName) && crmName != null)
                {
                    var crmNameStr = crmName.ToString();
                    if (!string.IsNullOrEmpty(crmNameStr))
                    {
                        var existingCompany = await _context.Companies
                            .Where(c => c.Title != null && c.Title.ToLower() == crmNameStr.ToLower())
                            .Select(c => (long?)c.Id)
                            .FirstOrDefaultAsync();
                        if (existingCompany.HasValue)
                            return existingCompany;
                    }
                }
                break;

            case AuthScapeEntityType.Location:
                // Match by location title
                var locationTitle = authScapeFields.GetValueOrDefault("Title")?.ToString();
                if (!string.IsNullOrEmpty(locationTitle))
                {
                    var existingLocation = await _context.Locations
                        .Where(l => l.Title != null && l.Title.ToLower() == locationTitle.ToLower())
                        .Select(l => (long?)l.Id)
                        .FirstOrDefaultAsync();
                    if (existingLocation.HasValue)
                        return existingLocation;
                }
                break;
        }

        return null;
    }

    private async Task<List<long>> GetAuthScapeEntityIdsAsync(AuthScapeEntityType entityType)
    {
        return entityType switch
        {
            AuthScapeEntityType.User => await _context.Users.Select(u => u.Id).ToListAsync(),
            AuthScapeEntityType.Company => await _context.Companies.Select(c => c.Id).ToListAsync(),
            AuthScapeEntityType.Location => await _context.Locations.Select(l => l.Id).ToListAsync(),
            _ => new List<long>()
        };
    }

    private async Task<Dictionary<string, object?>?> GetAuthScapeEntityDataAsync(AuthScapeEntityType entityType, long entityId)
    {
        return entityType switch
        {
            AuthScapeEntityType.User => await GetUserDataAsync(entityId),
            AuthScapeEntityType.Company => await GetCompanyDataAsync(entityId),
            AuthScapeEntityType.Location => await GetLocationDataAsync(entityId),
            _ => null
        };
    }

    private async Task<Dictionary<string, object?>?> GetUserDataAsync(long userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;

        return new Dictionary<string, object?>
        {
            ["FirstName"] = user.FirstName,
            ["LastName"] = user.LastName,
            ["Email"] = user.Email,
            ["PhoneNumber"] = user.PhoneNumber,
            ["PhotoUri"] = user.PhotoUri,
            ["locale"] = user.locale,
            ["Culture"] = user.Culture,
            ["Country"] = user.Country,
            ["TimeZoneId"] = user.TimeZoneId,
            ["IsActive"] = user.IsActive,
            ["Created"] = user.Created,
            ["LastLoggedIn"] = user.LastLoggedIn,
            // Relationship fields for CRM sync
            ["CompanyId"] = user.CompanyId,
            ["LocationId"] = user.LocationId
        };
    }

    private async Task<Dictionary<string, object?>?> GetCompanyDataAsync(long companyId)
    {
        var company = await _context.Companies.FindAsync(companyId);
        if (company == null) return null;

        return new Dictionary<string, object?>
        {
            ["Title"] = company.Title,
            ["Logo"] = company.Logo,
            ["Description"] = company.Description,
            ["IsDeactivated"] = company.IsDeactivated
        };
    }

    private async Task<Dictionary<string, object?>?> GetLocationDataAsync(long locationId)
    {
        var location = await _context.Locations.FindAsync(locationId);
        if (location == null) return null;

        return new Dictionary<string, object?>
        {
            ["Title"] = location.Title,
            ["Address"] = location.Address,
            ["City"] = location.City,
            ["State"] = location.State,
            ["ZipCode"] = location.ZipCode,
            ["lat"] = location.lat,
            ["lng"] = location.lng,
            ["IsDeactivated"] = location.IsDeactivated,
            // Relationship fields for CRM sync
            ["CompanyId"] = location.CompanyId
        };
    }

    private Dictionary<string, object?> MapAuthScapeToCrm(Dictionary<string, object?> authScapeData, IEnumerable<CrmFieldMapping> fieldMappings)
    {
        var crmFields = new Dictionary<string, object?>();

        // Known Dynamics 365 lookup fields that should NOT be mapped directly
        // These must be handled via relationship mappings using @odata.bind format
        var dynamicsLookupFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "accountid", "parentcustomerid", "parentaccountid", "ownerid", "owninguser",
            "owningteam", "owningbusinessunit", "transactioncurrencyid", "createdby",
            "modifiedby", "createdonbehalfby", "modifiedonbehalfby", "originatingleadid",
            "preferredsystemuserid", "preferredserviceid", "slaid", "slainvokedid",
            "parentcontactid", "masterid", "primarycontactid"
        };

        foreach (var mapping in fieldMappings)
        {
            if (mapping.SyncDirection == CrmSyncDirection.Inbound)
                continue; // Skip inbound-only fields

            var crmFieldName = mapping.CrmField?.Trim();
            if (string.IsNullOrEmpty(crmFieldName))
                continue;

            // Skip known Dynamics lookup fields - these must use relationship mappings with @odata.bind
            if (dynamicsLookupFields.Contains(crmFieldName))
            {
                _logger.LogDebug("Skipping lookup field '{CrmField}' in direct mapping - use relationship mapping instead", crmFieldName);
                continue;
            }

            if (authScapeData.TryGetValue(mapping.AuthScapeField, out var value))
            {
                crmFields[crmFieldName] = value;
            }
        }

        return crmFields;
    }

    private Dictionary<string, object?> MapCrmToAuthScape(CrmRecord crmRecord, IEnumerable<CrmFieldMapping> fieldMappings)
    {
        var authScapeFields = new Dictionary<string, object?>();

        foreach (var mapping in fieldMappings)
        {
            if (mapping.SyncDirection == CrmSyncDirection.Outbound)
                continue; // Skip outbound-only fields

            if (crmRecord.Fields.TryGetValue(mapping.CrmField, out var value))
            {
                authScapeFields[mapping.AuthScapeField] = value;
            }
        }

        return authScapeFields;
    }

    /// <summary>
    /// Resolves relationship fields for outbound sync by looking up CRM external IDs.
    /// Automatically discovers the correct CRM lookup field from metadata when the configured
    /// field is a primary key or invalid.
    /// For example: User.CompanyId=5 → Contact.parentcustomerid="{CRM Account GUID}"
    /// </summary>
    private async Task ResolveOutboundRelationshipsAsync(
        CrmConnection connection,
        CrmEntityMapping mapping,
        Dictionary<string, object?> entityData,
        Dictionary<string, object?> crmFields)
    {
        var relationshipMappings = mapping.RelationshipMappings?
            .Where(r => r.IsEnabled &&
                       (r.SyncDirection == CrmSyncDirection.Outbound ||
                        r.SyncDirection == CrmSyncDirection.Bidirectional))
            .ToList();

        if (relationshipMappings == null || !relationshipMappings.Any())
            return;

        var provider = _providerFactory.GetProvider(connection);

        foreach (var relMapping in relationshipMappings)
        {
            try
            {
                // Auto-resolve the correct lookup field from CRM metadata
                var lookupField = await ResolveLookupFieldAsync(
                    provider, connection, mapping.CrmEntityName, relMapping);

                if (string.IsNullOrEmpty(lookupField))
                {
                    _logger.LogWarning("Could not resolve lookup field for relationship {AuthScapeField} → {CrmRelatedEntity} on {CrmEntity}",
                        relMapping.AuthScapeField, relMapping.CrmRelatedEntityName, mapping.CrmEntityName);
                    continue;
                }

                // Get the AuthScape relationship ID (e.g., CompanyId value)
                if (!entityData.TryGetValue(relMapping.AuthScapeField, out var relatedIdValue) || relatedIdValue == null)
                {
                    // Relationship is null in AuthScape
                    if (relMapping.SyncNullValues)
                    {
                        crmFields[$"{lookupField}@odata.bind"] = null;
                    }
                    continue;
                }

                // Convert to long (the AuthScape entity ID)
                if (!long.TryParse(relatedIdValue.ToString(), out var relatedEntityId))
                {
                    _logger.LogWarning("Could not parse relationship ID value '{Value}' for field {Field}",
                        relatedIdValue, relMapping.AuthScapeField);
                    continue;
                }

                // Look up the CRM ID for the related entity
                var relatedCrmId = await _context.CrmExternalIds
                    .Where(e => e.CrmConnectionId == connection.Id &&
                               e.AuthScapeEntityType == relMapping.RelatedAuthScapeEntityType &&
                               e.AuthScapeEntityId == relatedEntityId &&
                               e.CrmEntityName == relMapping.CrmRelatedEntityName)
                    .Select(e => e.CrmEntityId)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(relatedCrmId))
                {
                    // For Dynamics lookup fields, we use the @odata.bind format
                    // e.g., "parentcustomerid@odata.bind": "/accounts(guid)"
                    var entitySetName = GetEntitySetName(relMapping.CrmRelatedEntityName);
                    crmFields[$"{lookupField}@odata.bind"] = $"/{entitySetName}({relatedCrmId})";

                    _logger.LogDebug("Resolved outbound relationship: {AuthScapeField}={EntityId} → {CrmField}={CrmId}",
                        relMapping.AuthScapeField, relatedEntityId, lookupField, relatedCrmId);
                }
                else
                {
                    _logger.LogDebug("No CRM external ID found for {EntityType} {EntityId} - relationship {CrmField} will be null",
                        relMapping.RelatedAuthScapeEntityType, relatedEntityId, lookupField);

                    if (relMapping.SyncNullValues)
                    {
                        crmFields[$"{lookupField}@odata.bind"] = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error resolving outbound relationship for {AuthScapeField} → {CrmField}",
                    relMapping.AuthScapeField, relMapping.CrmLookupField);
            }
        }
    }

    // Cache for resolved lookup fields: key = "entityName:targetEntityName", value = resolved lookup field name
    private readonly Dictionary<string, string?> _lookupFieldCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the correct CRM navigation property name for a relationship mapping.
    /// Always queries the CRM metadata to find the correct OData navigation property,
    /// because Dynamics polymorphic lookups (Customer, Owner) require the navigation
    /// property name (e.g., "parentcustomerid_account") rather than the attribute name
    /// (e.g., "parentcustomerid") in @odata.bind payloads.
    /// </summary>
    private async Task<string?> ResolveLookupFieldAsync(
        ICrmProvider provider,
        CrmConnection connection,
        string sourceEntityName,
        CrmRelationshipMapping relMapping)
    {
        // Always resolve from metadata to get the correct navigation property name
        var cacheKey = $"{sourceEntityName}:{relMapping.CrmRelatedEntityName}";
        if (_lookupFieldCache.TryGetValue(cacheKey, out var cached))
            return cached;

        _logger.LogInformation("Auto-discovering lookup field on '{SourceEntity}' that targets '{TargetEntity}'",
            sourceEntityName, relMapping.CrmRelatedEntityName);

        try
        {
            var lookupFields = await provider.GetLookupFieldsAsync(connection, sourceEntityName, relMapping.CrmRelatedEntityName);

            if (lookupFields.Count == 1)
            {
                var resolved = lookupFields[0].LogicalName;
                _lookupFieldCache[cacheKey] = resolved;
                _logger.LogInformation("Auto-resolved lookup field: '{SourceEntity}' → '{TargetEntity}' uses '{LookupField}'",
                    sourceEntityName, relMapping.CrmRelatedEntityName, resolved);
                return resolved;
            }
            else if (lookupFields.Count > 1)
            {
                // Multiple lookup fields target the same entity
                // Prefer the one matching the configured CrmLookupField if it exists in the results
                var configuredField = relMapping.CrmLookupField?.Trim();
                var matchingConfigured = !string.IsNullOrEmpty(configuredField)
                    ? lookupFields.FirstOrDefault(f => f.LogicalName.StartsWith(configuredField, StringComparison.OrdinalIgnoreCase))
                    : null;

                var resolved = matchingConfigured?.LogicalName
                            ?? lookupFields[0].LogicalName;
                _lookupFieldCache[cacheKey] = resolved;
                _logger.LogInformation("Multiple lookup fields found on '{SourceEntity}' targeting '{TargetEntity}': [{Fields}]. Using '{LookupField}'",
                    sourceEntityName, relMapping.CrmRelatedEntityName,
                    string.Join(", ", lookupFields.Select(f => f.LogicalName)), resolved);
                return resolved;
            }
            else
            {
                _logger.LogWarning("No lookup field found on '{SourceEntity}' that targets '{TargetEntity}'",
                    sourceEntityName, relMapping.CrmRelatedEntityName);
                _lookupFieldCache[cacheKey] = null;
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error auto-discovering lookup field on '{SourceEntity}' targeting '{TargetEntity}'",
                sourceEntityName, relMapping.CrmRelatedEntityName);
            return null;
        }
    }

    /// <summary>
    /// Resolves a single relationship mapping for an AuthScape entity to get the CRM lookup value.
    /// Used for relationship-only syncs where we don't have the full entity data.
    /// </summary>
    private async Task<string?> ResolveRelationshipValueAsync(
        CrmConnection connection,
        AuthScapeEntityType entityType,
        long authScapeEntityId,
        CrmRelationshipMapping relMapping)
    {
        try
        {
            // Get the related entity ID from the AuthScape entity
            long? relatedEntityId = null;

            // Normalize field name - handle both "Company" and "CompanyId", "Location" and "LocationId"
            var normalizedField = relMapping.AuthScapeField?.Replace("Id", "") ?? "";

            switch (entityType)
            {
                case AuthScapeEntityType.User:
                    var user = await _context.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == authScapeEntityId);
                    if (user != null)
                    {
                        relatedEntityId = normalizedField switch
                        {
                            "Company" => user.CompanyId,
                            "Location" => user.LocationId,
                            _ => null
                        };
                    }
                    break;

                case AuthScapeEntityType.Location:
                    var location = await _context.Locations.AsNoTracking()
                        .FirstOrDefaultAsync(l => l.Id == authScapeEntityId);
                    if (location != null)
                    {
                        relatedEntityId = normalizedField switch
                        {
                            "Company" => location.CompanyId,
                            _ => null
                        };
                    }
                    break;

                case AuthScapeEntityType.Company:
                    // Company doesn't have relationships to other entities in the standard model
                    break;
            }

            _logger.LogDebug("ResolveRelationshipValue: EntityType={EntityType}, EntityId={EntityId}, Field={Field}, NormalizedField={NormalizedField}, RelatedEntityId={RelatedEntityId}",
                entityType, authScapeEntityId, relMapping.AuthScapeField, normalizedField, relatedEntityId);

            if (relatedEntityId == null)
            {
                _logger.LogDebug("RelatedEntityId is null for {EntityType} {EntityId} field {Field} - user may not have this relationship set",
                    entityType, authScapeEntityId, relMapping.AuthScapeField);
                return null;
            }

            // Look up the CRM ID for the related entity
            _logger.LogDebug("Looking up CrmExternalId: ConnectionId={ConnectionId}, RelatedEntityType={RelatedEntityType}, RelatedEntityId={RelatedEntityId}, CrmEntityName={CrmEntityName}",
                connection.Id, relMapping.RelatedAuthScapeEntityType, relatedEntityId, relMapping.CrmRelatedEntityName);

            var relatedCrmExternalId = await _context.CrmExternalIds
                .Where(e => e.CrmConnectionId == connection.Id &&
                           e.AuthScapeEntityType == relMapping.RelatedAuthScapeEntityType &&
                           e.AuthScapeEntityId == relatedEntityId &&
                           e.CrmEntityName == relMapping.CrmRelatedEntityName)
                .Select(e => e.CrmEntityId)
                .FirstOrDefaultAsync();

            if (relatedCrmExternalId != null)
            {
                // Return the odata.bind format for Dynamics lookups
                // Use the entity set name (typically plural) - for standard entities this is usually entityname + "s"
                // Format: "/accounts(guid)" - the leading slash IS required for @odata.bind
                var entitySetName = GetEntitySetName(relMapping.CrmRelatedEntityName);
                var result = $"/{entitySetName}({relatedCrmExternalId})";
                _logger.LogDebug("Found CRM external ID: {CrmExternalId}, returning {Result}", relatedCrmExternalId, result);
                return result;
            }

            _logger.LogWarning("No CrmExternalId found for {RelatedEntityType} {RelatedEntityId} -> {CrmEntityName}. " +
                "This means the related entity (e.g., Location) has not been synced to CRM yet. " +
                "Make sure to sync Locations to accounts before syncing User relationships.",
                relMapping.RelatedAuthScapeEntityType, relatedEntityId, relMapping.CrmRelatedEntityName);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error resolving relationship value for {EntityType} {EntityId} field {Field}",
                entityType, authScapeEntityId, relMapping.AuthScapeField);
            return null;
        }
    }

    /// <summary>
    /// Gets the entity set name (plural form) for a Dynamics entity logical name
    /// Most entities just add 's', but some have special pluralization
    /// </summary>
    private static string GetEntitySetName(string logicalName)
    {
        // Handle common special cases in Dynamics
        return logicalName.ToLowerInvariant() switch
        {
            "opportunity" => "opportunities",
            "category" => "categories",
            "territory" => "territories",
            "currency" => "currencies",
            "transactioncurrency" => "transactioncurrencies",
            "activityparty" => "activityparties",
            "customeraddress" => "customeraddresses",
            "businessunit" => "businessunits",
            "systemuser" => "systemusers",
            // Default: just add 's' for most entities (contact->contacts, account->accounts, etc.)
            _ => logicalName + "s"
        };
    }

    /// <summary>
    /// Resolves relationship fields for inbound sync by looking up AuthScape entity IDs from CRM lookup values
    /// For example: Contact.parentcustomerid="{CRM GUID}" → User.CompanyId=5
    /// </summary>
    private async Task ResolveInboundRelationshipsAsync(
        CrmConnection connection,
        CrmEntityMapping mapping,
        CrmRecord crmRecord,
        Dictionary<string, object?> authScapeFields)
    {
        var relationshipMappings = mapping.RelationshipMappings?
            .Where(r => r.IsEnabled &&
                       (r.SyncDirection == CrmSyncDirection.Inbound ||
                        r.SyncDirection == CrmSyncDirection.Bidirectional))
            .ToList();

        if (relationshipMappings == null || !relationshipMappings.Any())
            return;

        foreach (var relMapping in relationshipMappings)
        {
            try
            {
                // For Dynamics, lookup values come in as "_fieldname_value" format
                var lookupFieldKey = $"_{relMapping.CrmLookupField}_value";

                // Also try the direct field name for other CRM systems
                string? crmRelatedId = null;
                if (crmRecord.Fields.TryGetValue(lookupFieldKey, out var lookupValue) && lookupValue != null)
                {
                    crmRelatedId = lookupValue.ToString();
                }
                else if (crmRecord.Fields.TryGetValue(relMapping.CrmLookupField, out var directValue) && directValue != null)
                {
                    crmRelatedId = directValue.ToString();
                }

                if (string.IsNullOrEmpty(crmRelatedId))
                {
                    // Relationship is null in CRM
                    if (relMapping.SyncNullValues)
                    {
                        authScapeFields[relMapping.AuthScapeField] = null;
                    }
                    continue;
                }

                // Look up the AuthScape entity ID for the related CRM record
                var relatedAuthScapeId = await _context.CrmExternalIds
                    .Where(e => e.CrmConnectionId == connection.Id &&
                               e.CrmEntityName == relMapping.CrmRelatedEntityName &&
                               e.CrmEntityId == crmRelatedId &&
                               e.AuthScapeEntityType == relMapping.RelatedAuthScapeEntityType)
                    .Select(e => (long?)e.AuthScapeEntityId)
                    .FirstOrDefaultAsync();

                if (relatedAuthScapeId.HasValue)
                {
                    authScapeFields[relMapping.AuthScapeField] = relatedAuthScapeId.Value;

                    _logger.LogDebug("Resolved inbound relationship: {CrmField}={CrmId} → {AuthScapeField}={EntityId}",
                        relMapping.CrmLookupField, crmRelatedId, relMapping.AuthScapeField, relatedAuthScapeId.Value);
                }
                else
                {
                    _logger.LogDebug("No AuthScape entity found for CRM {CrmEntity} {CrmId} - relationship {AuthScapeField} will be null",
                        relMapping.CrmRelatedEntityName, crmRelatedId, relMapping.AuthScapeField);

                    if (relMapping.SyncNullValues)
                    {
                        authScapeFields[relMapping.AuthScapeField] = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error resolving inbound relationship for {CrmField} → {AuthScapeField}",
                    relMapping.CrmLookupField, relMapping.AuthScapeField);
            }
        }
    }

    private async Task UpdateAuthScapeEntityAsync(AuthScapeEntityType entityType, long entityId, Dictionary<string, object?> fields)
    {
        switch (entityType)
        {
            case AuthScapeEntityType.User:
                await UpdateUserAsync(entityId, fields);
                break;
            case AuthScapeEntityType.Company:
                await UpdateCompanyAsync(entityId, fields);
                break;
            case AuthScapeEntityType.Location:
                await UpdateLocationAsync(entityId, fields);
                break;
        }
    }

    private async Task UpdateUserAsync(long userId, Dictionary<string, object?> fields)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;

        // Only update fields if the value is not null - preserve existing values otherwise
        if (fields.TryGetValue("FirstName", out var firstName) && firstName != null)
        {
            var firstNameStr = firstName.ToString()?.Trim();
            if (!string.IsNullOrEmpty(firstNameStr))
                user.FirstName = firstNameStr;
        }

        if (fields.TryGetValue("LastName", out var lastName) && lastName != null)
        {
            var lastNameStr = lastName.ToString()?.Trim();
            if (!string.IsNullOrEmpty(lastNameStr))
                user.LastName = lastNameStr;
        }

        if (fields.TryGetValue("PhoneNumber", out var phone) && phone != null)
            user.PhoneNumber = phone.ToString();

        if (fields.TryGetValue("PhotoUri", out var photo) && photo != null)
            user.PhotoUri = photo.ToString();

        // Handle IsActive - this is always set explicitly (true or false)
        if (fields.TryGetValue("IsActive", out var isActive) && isActive != null)
        {
            if (isActive is bool isActiveBool)
                user.IsActive = isActiveBool;
            else if (bool.TryParse(isActive.ToString(), out var parsed))
                user.IsActive = parsed;
        }

        // Handle relationship fields (CompanyId, LocationId)
        if (fields.TryGetValue("CompanyId", out var companyId))
        {
            if (companyId == null)
                user.CompanyId = null;
            else if (companyId is long companyIdLong)
                user.CompanyId = companyIdLong;
            else if (long.TryParse(companyId.ToString(), out var parsedCompanyId))
                user.CompanyId = parsedCompanyId;
        }

        if (fields.TryGetValue("LocationId", out var locationId))
        {
            if (locationId == null)
                user.LocationId = null;
            else if (locationId is long locationIdLong)
                user.LocationId = locationIdLong;
            else if (long.TryParse(locationId.ToString(), out var parsedLocationId))
                user.LocationId = parsedLocationId;
        }

        await _context.SaveChangesAsync();
    }

    private async Task UpdateCompanyAsync(long companyId, Dictionary<string, object?> fields)
    {
        var company = await _context.Companies.FindAsync(companyId);
        if (company == null) return;

        if (fields.TryGetValue("Title", out var title)) company.Title = title?.ToString() ?? "";
        if (fields.TryGetValue("Logo", out var logo)) company.Logo = logo?.ToString();
        if (fields.TryGetValue("Description", out var desc)) company.Description = desc?.ToString();

        await _context.SaveChangesAsync();
    }

    private async Task UpdateLocationAsync(long locationId, Dictionary<string, object?> fields)
    {
        var location = await _context.Locations.FindAsync(locationId);
        if (location == null) return;

        if (fields.TryGetValue("Title", out var title)) location.Title = title?.ToString() ?? "";
        if (fields.TryGetValue("Address", out var address)) location.Address = address?.ToString();
        if (fields.TryGetValue("City", out var city)) location.City = city?.ToString();
        if (fields.TryGetValue("State", out var state)) location.State = state?.ToString();
        if (fields.TryGetValue("ZipCode", out var zip)) location.ZipCode = zip?.ToString();

        // Handle relationship fields (CompanyId)
        if (fields.TryGetValue("CompanyId", out var companyId))
        {
            if (companyId == null)
                location.CompanyId = null;
            else if (companyId is long companyIdLong)
                location.CompanyId = companyIdLong;
            else if (long.TryParse(companyId.ToString(), out var parsedCompanyId))
                location.CompanyId = parsedCompanyId;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<long> CreateAuthScapeEntityAsync(AuthScapeEntityType entityType, Dictionary<string, object?> fields)
    {
        return entityType switch
        {
            AuthScapeEntityType.User => await CreateUserAsync(fields),
            AuthScapeEntityType.Company => await CreateCompanyAsync(fields),
            AuthScapeEntityType.Location => await CreateLocationAsync(fields),
            _ => throw new ArgumentException($"Unknown entity type: {entityType}")
        };
    }

    private async Task<long> CreateUserAsync(Dictionary<string, object?> fields)
    {
        // Get email from mapped fields
        var email = fields.GetValueOrDefault("Email")?.ToString()?.Trim();

        // If no email, generate a placeholder (this contact didn't have email mapped)
        if (string.IsNullOrEmpty(email))
        {
            var guid = Guid.NewGuid().ToString("N")[..12];
            email = $"crm-{guid}@imported.local";
        }

        // Normalize email to lowercase
        email = email.ToLowerInvariant();

        // Check if user with this email already exists (double-check for safety)
        var existingUser = await _context.Users
            .Where(u => u.Email != null && u.Email.ToLower() == email)
            .FirstOrDefaultAsync();

        if (existingUser != null)
        {
            _logger.LogWarning("User with email {Email} already exists (ID: {UserId}), skipping creation", email, existingUser.Id);
            return existingUser.Id;
        }

        // Get first and last name, defaulting to empty string if null (database requires non-null)
        var firstName = fields.GetValueOrDefault("FirstName")?.ToString()?.Trim();
        var lastName = fields.GetValueOrDefault("LastName")?.ToString()?.Trim();

        // If both names are empty, try to extract from email
        if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
        {
            var emailPart = email.Split('@')[0];
            firstName = emailPart; // Use email prefix as fallback name
        }

        // Ensure we have at least an empty string (not null) for required fields
        firstName = firstName ?? "";
        lastName = lastName ?? "";

        // Determine IsActive from fields (defaults to true if not specified)
        var isActive = true;
        if (fields.TryGetValue("IsActive", out var isActiveValue) && isActiveValue != null)
        {
            if (isActiveValue is bool isActiveBool)
                isActive = isActiveBool;
            else if (bool.TryParse(isActiveValue.ToString(), out var parsed))
                isActive = parsed;
        }

        var user = new AppUser
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            UserName = email, // Use same email for username
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            PhoneNumber = fields.GetValueOrDefault("PhoneNumber")?.ToString(),
            PhotoUri = fields.GetValueOrDefault("PhotoUri")?.ToString(),
            Created = DateTimeOffset.UtcNow,
            EmailConfirmed = true, // CRM contacts are considered confirmed
            IsActive = isActive
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new user from CRM: {Email} (ID: {UserId})", email, user.Id);
        return user.Id;
    }

    private async Task<long> CreateCompanyAsync(Dictionary<string, object?> fields)
    {
        var company = new Company
        {
            Title = fields.GetValueOrDefault("Title")?.ToString() ?? "Imported Company",
            Logo = fields.GetValueOrDefault("Logo")?.ToString(),
            Description = fields.GetValueOrDefault("Description")?.ToString()
        };

        _context.Companies.Add(company);
        await _context.SaveChangesAsync();
        return company.Id;
    }

    private async Task<long> CreateLocationAsync(Dictionary<string, object?> fields)
    {
        var location = new Location
        {
            Title = fields.GetValueOrDefault("Title")?.ToString() ?? "Imported Location",
            Address = fields.GetValueOrDefault("Address")?.ToString(),
            City = fields.GetValueOrDefault("City")?.ToString(),
            State = fields.GetValueOrDefault("State")?.ToString(),
            ZipCode = fields.GetValueOrDefault("ZipCode")?.ToString()
        };

        _context.Locations.Add(location);
        await _context.SaveChangesAsync();
        return location.Id;
    }

    private async Task LogSync(long connectionId, long? mappingId, AuthScapeEntityType? entityType, long? entityId,
        string? crmEntityName, string? crmEntityId, string direction, CrmSyncAction action, CrmSyncStatus status,
        long durationMs, string? errorMessage = null)
    {
        var log = new CrmSyncLog
        {
            CrmConnectionId = connectionId,
            CrmEntityMappingId = mappingId,
            AuthScapeEntityType = entityType,
            AuthScapeEntityId = entityId,
            CrmEntityName = crmEntityName,
            CrmEntityId = crmEntityId,
            Direction = direction,
            Action = action,
            Status = status,
            DurationMs = durationMs,
            ErrorMessage = errorMessage,
            SyncedAt = DateTimeOffset.UtcNow
        };

        _context.CrmSyncLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    #endregion
}
