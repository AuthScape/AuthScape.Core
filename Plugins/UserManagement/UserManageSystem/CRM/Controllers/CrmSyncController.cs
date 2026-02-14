using AuthScape.UserManageSystem.CRM.Interfaces;
using AuthScape.UserManageSystem.Models.CRM.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenIddict.Validation.AspNetCore;

namespace AuthScape.UserManageSystem.CRM.Controllers;

/// <summary>
/// API controller for CRM sync operations
/// </summary>
[ApiController]
[Route("api/UserManagement/[action]")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class CrmSyncController : ControllerBase
{
    private readonly ICrmSyncService _syncService;
    private readonly ILogger<CrmSyncController> _logger;

    public CrmSyncController(
        ICrmSyncService syncService,
        ILogger<CrmSyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a full sync for a connection
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResultDto>> TriggerCrmFullSync(long connectionId)
    {
        _logger.LogInformation("Starting full sync for connection {ConnectionId}", connectionId);

        var result = await _syncService.SyncAllAsync(connectionId);

        return Ok(MapToDto(result));
    }

    /// <summary>
    /// Triggers an incremental sync for a connection (only changes since last sync)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResultDto>> TriggerCrmIncrementalSync(long connectionId)
    {
        _logger.LogInformation("Starting incremental sync for connection {ConnectionId}", connectionId);

        var result = await _syncService.SyncIncrementalAsync(connectionId);

        return Ok(MapToDto(result));
    }

    /// <summary>
    /// Triggers a sync for a specific entity mapping (e.g., just Contact → User)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResultDto>> TriggerCrmEntityMappingSync(
        long entityMappingId,
        [FromQuery] bool fullSync = true)
    {
        _logger.LogInformation("Starting sync for entity mapping {EntityMappingId} (fullSync={FullSync})",
            entityMappingId, fullSync);

        var result = await _syncService.SyncEntityMappingAsync(entityMappingId, fullSync);

        return Ok(MapToDto(result));
    }

    /// <summary>
    /// Triggers a relationship-only sync for an entity mapping (faster than full sync)
    /// Only updates lookup fields without syncing all field data
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResultDto>> TriggerCrmRelationshipSync(long entityMappingId)
    {
        _logger.LogInformation("Starting relationship sync for entity mapping {EntityMappingId}", entityMappingId);

        var result = await _syncService.SyncRelationshipsAsync(entityMappingId);

        return Ok(MapToDto(result));
    }

    /// <summary>
    /// Pulls location/account assignments from CRM Contacts and assigns LocationId to AuthScape users.
    /// Optionally creates a Company from the Location's title when autoCreateCompany is true.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResultDto>> SyncCrmUserLocations(long connectionId, [FromQuery] bool autoCreateCompany = false)
    {
        _logger.LogInformation("Starting user location sync from CRM for connection {ConnectionId}, autoCreateCompany={AutoCreateCompany}",
            connectionId, autoCreateCompany);

        var result = await _syncService.SyncUserLocationsFromCrmAsync(connectionId, autoCreateCompany);

        return Ok(MapToDto(result));
    }

    /// <summary>
    /// Gets diagnostic information about CRM external ID mappings for debugging sync issues
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CrmSyncDiagnosticsDto>> GetCrmSyncDiagnostics(long connectionId)
    {
        var diagnostics = await _syncService.GetSyncDiagnosticsAsync(connectionId);
        return Ok(diagnostics);
    }

    /// <summary>
    /// Triggers an outbound sync (AuthScape -> CRM) for a specific entity
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResultDto>> TriggerCrmOutboundSync(
        long connectionId,
        [FromQuery] AuthScapeEntityType entityType,
        [FromQuery] long entityId)
    {
        _logger.LogInformation("Starting outbound sync for {EntityType} {EntityId} to connection {ConnectionId}",
            entityType, entityId, connectionId);

        var result = await _syncService.SyncOutboundAsync(connectionId, entityType, entityId);

        return Ok(MapToDto(result));
    }

    /// <summary>
    /// Triggers an inbound sync (CRM -> AuthScape) for a specific CRM record
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResultDto>> TriggerCrmInboundSync(
        long connectionId,
        [FromQuery] string crmEntityName,
        [FromQuery] string crmRecordId)
    {
        _logger.LogInformation("Starting inbound sync for {CrmEntity} {CrmRecordId} from connection {ConnectionId}",
            crmEntityName, crmRecordId, connectionId);

        var result = await _syncService.SyncInboundAsync(connectionId, crmEntityName, crmRecordId);

        return Ok(MapToDto(result));
    }

    /// <summary>
    /// Gets sync logs for a connection
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SyncLogDto>>> GetCrmSyncLogs(
        long connectionId,
        [FromQuery] int? limit = 100,
        [FromQuery] CrmSyncStatus? status = null,
        [FromQuery] DateTimeOffset? since = null)
    {
        var logs = await _syncService.GetSyncLogsAsync(connectionId, limit ?? 100, status, since);

        return Ok(logs.Select(l => new SyncLogDto
        {
            Id = l.Id,
            ConnectionId = l.CrmConnectionId,
            EntityMappingId = l.CrmEntityMappingId,
            AuthScapeEntityType = l.AuthScapeEntityType,
            AuthScapeEntityId = l.AuthScapeEntityId,
            CrmEntityName = l.CrmEntityName,
            CrmEntityId = l.CrmEntityId,
            Direction = l.Direction,
            Action = l.Action,
            Status = l.Status,
            ErrorMessage = l.ErrorMessage,
            ChangedFields = l.ChangedFields,
            SyncedAt = l.SyncedAt
        }));
    }

    /// <summary>
    /// Gets sync statistics for a connection
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SyncStatsDto>> GetCrmSyncStats(
        long connectionId,
        [FromQuery] DateTimeOffset? since = null)
    {
        var logs = await _syncService.GetSyncLogsAsync(connectionId, 10000, null, since);
        var logList = logs.ToList();

        return Ok(new SyncStatsDto
        {
            TotalSyncs = logList.Count,
            SuccessCount = logList.Count(l => l.Status == CrmSyncStatus.Success),
            FailedCount = logList.Count(l => l.Status == CrmSyncStatus.Failed),
            ConflictCount = logList.Count(l => l.Status == CrmSyncStatus.Conflict),
            SkippedCount = logList.Count(l => l.Status == CrmSyncStatus.Skipped),
            InboundCount = logList.Count(l => l.Direction == "Inbound"),
            OutboundCount = logList.Count(l => l.Direction == "Outbound"),
            CreateCount = logList.Count(l => l.Action == CrmSyncAction.Create),
            UpdateCount = logList.Count(l => l.Action == CrmSyncAction.Update),
            DeleteCount = logList.Count(l => l.Action == CrmSyncAction.Delete),
            LastSyncAt = logList.OrderByDescending(l => l.SyncedAt).FirstOrDefault()?.SyncedAt
        });
    }

    /// <summary>
    /// Gets external ID mappings for a connection
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExternalIdDto>>> GetCrmExternalIds(
        long connectionId,
        [FromQuery] AuthScapeEntityType? entityType = null,
        [FromQuery] int? limit = 100)
    {
        var externalIds = await _syncService.GetExternalIdsAsync(connectionId, entityType, limit ?? 100);

        return Ok(externalIds.Select(e => new ExternalIdDto
        {
            Id = e.Id,
            ConnectionId = e.CrmConnectionId,
            AuthScapeEntityType = e.AuthScapeEntityType,
            AuthScapeEntityId = e.AuthScapeEntityId,
            CrmEntityName = e.CrmEntityName,
            CrmEntityId = e.CrmEntityId,
            LastSyncedAt = e.LastSyncedAt,
            LastSyncDirection = e.LastSyncDirection
        }));
    }

    /// <summary>
    /// Clears sync logs for a connection
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ClearCrmSyncLogs(
        long connectionId,
        [FromQuery] DateTimeOffset? olderThan = null)
    {
        await _syncService.ClearSyncLogsAsync(connectionId, olderThan);
        _logger.LogInformation("Cleared sync logs for connection {ConnectionId}", connectionId);

        return Ok();
    }

    /// <summary>
    /// Detects duplicate records between AuthScape and CRM without modifying any data.
    /// Returns duplicates within each system and unlinked matches that would be auto-linked on next sync.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CrmDuplicateDetectionResultDto>> DetectCrmDuplicates(long connectionId)
    {
        _logger.LogInformation("Running duplicate detection for connection {ConnectionId}", connectionId);

        var result = await _syncService.DetectDuplicatesAsync(connectionId);

        return Ok(new CrmDuplicateDetectionResultDto
        {
            HasDuplicates = result.HasDuplicates,
            DuplicatesInCrm = result.DuplicatesInCrm.Select(d => new DuplicateRecordDto
            {
                EntityType = d.EntityType,
                Identifier = d.Identifier,
                Side = d.Side,
                RecordIds = d.RecordIds
            }).ToList(),
            DuplicatesInAuthScape = result.DuplicatesInAuthScape.Select(d => new DuplicateRecordDto
            {
                EntityType = d.EntityType,
                Identifier = d.Identifier,
                Side = d.Side,
                RecordIds = d.RecordIds
            }).ToList(),
            UnlinkedMatches = result.UnlinkedMatches.Select(m => new UnlinkedMatchDto
            {
                EntityType = m.EntityType,
                Identifier = m.Identifier,
                AuthScapeEntityId = m.AuthScapeEntityId,
                CrmEntityId = m.CrmEntityId
            }).ToList(),
            Summary = result.Summary
        });
    }

    #region Private Helpers

    private static SyncResultDto MapToDto(CrmSyncResult result)
    {
        return new SyncResultDto
        {
            Success = result.Success,
            Message = result.Message,
            SyncId = result.SyncId,
            Stats = new SyncStatsDto
            {
                TotalSyncs = result.Stats.TotalProcessed,
                SuccessCount = result.Stats.SuccessCount,
                FailedCount = result.Stats.FailedCount,
                ConflictCount = result.Stats.ConflictCount,
                SkippedCount = result.Stats.SkippedCount,
                InboundCount = result.Stats.InboundCount,
                OutboundCount = result.Stats.OutboundCount,
                CreateCount = result.Stats.CreatedCount,
                UpdateCount = result.Stats.UpdatedCount,
                DeleteCount = result.Stats.DeletedCount,
                DuplicatesDetectedCount = result.Stats.DuplicatesDetectedCount,
                LinkedByMatchCount = result.Stats.LinkedByMatchCount
            },
            Duplicates = result.Duplicates.Select(d => new DuplicateRecordDto
            {
                EntityType = d.EntityType,
                Identifier = d.Identifier,
                Side = d.Side,
                RecordIds = d.RecordIds
            }).ToList(),
            Errors = result.Errors
        };
    }

    #endregion
}

#region DTOs

public class SyncResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? SyncId { get; set; }
    public SyncStatsDto Stats { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<DuplicateRecordDto> Duplicates { get; set; } = new();
}

public class SyncStatsDto
{
    public int TotalSyncs { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int ConflictCount { get; set; }
    public int SkippedCount { get; set; }
    public int InboundCount { get; set; }
    public int OutboundCount { get; set; }
    public int CreateCount { get; set; }
    public int UpdateCount { get; set; }
    public int DeleteCount { get; set; }
    public int DuplicatesDetectedCount { get; set; }
    public int LinkedByMatchCount { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
}

public class SyncLogDto
{
    public long Id { get; set; }
    public long ConnectionId { get; set; }
    public long? EntityMappingId { get; set; }
    public AuthScapeEntityType? AuthScapeEntityType { get; set; }
    public long? AuthScapeEntityId { get; set; }
    public string? CrmEntityName { get; set; }
    public string? CrmEntityId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public CrmSyncAction Action { get; set; }
    public CrmSyncStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ChangedFields { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
}

public class ExternalIdDto
{
    public long Id { get; set; }
    public long ConnectionId { get; set; }
    public AuthScapeEntityType AuthScapeEntityType { get; set; }
    public long AuthScapeEntityId { get; set; }
    public string CrmEntityName { get; set; } = string.Empty;
    public string CrmEntityId { get; set; } = string.Empty;
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastSyncDirection { get; set; }
}

public class CrmSyncDiagnosticsDto
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

public class DuplicateRecordDto
{
    public string EntityType { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public List<string> RecordIds { get; set; } = new();
}

public class UnlinkedMatchDto
{
    public string EntityType { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public long AuthScapeEntityId { get; set; }
    public string CrmEntityId { get; set; } = string.Empty;
}

public class CrmDuplicateDetectionResultDto
{
    public bool HasDuplicates { get; set; }
    public List<DuplicateRecordDto> DuplicatesInCrm { get; set; } = new();
    public List<DuplicateRecordDto> DuplicatesInAuthScape { get; set; } = new();
    public List<UnlinkedMatchDto> UnlinkedMatches { get; set; } = new();
    public string? Summary { get; set; }
}

#endregion
