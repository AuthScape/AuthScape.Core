using AuthScape.CRM.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AuthScape.CRM.Services;

/// <summary>
/// Service for reporting CRM sync progress via SignalR
/// </summary>
public interface ICrmSyncProgressService
{
    /// <summary>
    /// Creates a new sync operation and returns a unique sync ID
    /// </summary>
    string StartSync(long entityMappingId, string entityName, int totalRecords);

    /// <summary>
    /// Creates a new sync operation with connection-level grouping (for syncs not tied to a specific entity mapping)
    /// </summary>
    string StartSyncWithConnection(long entityMappingId, long? connectionId, string entityName, int totalRecords);

    /// <summary>
    /// Reports progress for a sync operation
    /// </summary>
    Task ReportProgressAsync(string syncId, int currentRecord, string? currentOperation = null);

    /// <summary>
    /// Reports a successful record sync
    /// </summary>
    Task ReportSuccessAsync(string syncId);

    /// <summary>
    /// Reports a failed record sync
    /// </summary>
    Task ReportFailureAsync(string syncId, string? errorMessage = null);

    /// <summary>
    /// Marks a sync operation as completed
    /// </summary>
    Task CompleteSyncAsync(string syncId, bool success, string? message = null);
}

public class CrmSyncProgressService : ICrmSyncProgressService
{
    private readonly IHubContext<CrmSyncHub> _hubContext;
    private readonly ILogger<CrmSyncProgressService> _logger;
    private readonly Dictionary<string, SyncProgressUpdate> _activesyncs = new();
    private readonly object _lock = new();

    public CrmSyncProgressService(
        IHubContext<CrmSyncHub> hubContext,
        ILogger<CrmSyncProgressService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public string StartSync(long entityMappingId, string entityName, int totalRecords)
    {
        return StartSyncWithConnection(entityMappingId, null, entityName, totalRecords);
    }

    /// <summary>
    /// Creates a new sync operation with connection-level grouping (for syncs not tied to a specific entity mapping)
    /// </summary>
    public string StartSyncWithConnection(long entityMappingId, long? connectionId, string entityName, int totalRecords)
    {
        var syncId = Guid.NewGuid().ToString("N")[..8]; // Short unique ID

        var progress = new SyncProgressUpdate
        {
            SyncId = syncId,
            EntityMappingId = entityMappingId,
            ConnectionId = connectionId,
            EntityName = entityName,
            TotalRecords = totalRecords,
            CurrentRecord = 0,
            PercentComplete = 0,
            Status = "InProgress",
            SuccessCount = 0,
            FailedCount = 0,
            CurrentOperation = $"Starting sync for {entityName}..."
        };

        lock (_lock)
        {
            _activesyncs[syncId] = progress;
        }

        _logger.LogInformation("Started sync {SyncId} for {EntityName} with {TotalRecords} records, broadcasting to entityMapping_{EntityMappingId}, connection_{ConnectionId}",
            syncId, entityName, totalRecords, entityMappingId, connectionId);

        // Broadcast initial progress immediately
        _ = BroadcastProgressAsync(progress);

        return syncId;
    }

    public async Task ReportProgressAsync(string syncId, int currentRecord, string? currentOperation = null)
    {
        SyncProgressUpdate? progress;
        lock (_lock)
        {
            if (!_activesyncs.TryGetValue(syncId, out progress))
                return;

            progress.CurrentRecord = currentRecord;
            progress.CurrentOperation = currentOperation;
            progress.PercentComplete = progress.TotalRecords > 0
                ? (int)Math.Round((double)currentRecord / progress.TotalRecords * 100)
                : 0;
        }

        await BroadcastProgressAsync(progress);
    }

    public async Task ReportSuccessAsync(string syncId)
    {
        SyncProgressUpdate? progress;
        lock (_lock)
        {
            if (!_activesyncs.TryGetValue(syncId, out progress))
                return;

            progress.SuccessCount++;
        }

        // Don't broadcast on every success to reduce traffic - batch updates via ReportProgressAsync
    }

    public async Task ReportFailureAsync(string syncId, string? errorMessage = null)
    {
        SyncProgressUpdate? progress;
        lock (_lock)
        {
            if (!_activesyncs.TryGetValue(syncId, out progress))
                return;

            progress.FailedCount++;
            if (errorMessage != null)
            {
                progress.ErrorMessage = errorMessage;
            }
        }

        await BroadcastProgressAsync(progress);
    }

    public async Task CompleteSyncAsync(string syncId, bool success, string? message = null)
    {
        SyncProgressUpdate? progress;
        lock (_lock)
        {
            if (!_activesyncs.TryGetValue(syncId, out progress))
                return;

            progress.Status = success ? "Completed" : "Failed";
            progress.PercentComplete = 100;
            progress.CurrentOperation = message;

            // Remove from active syncs after broadcasting
        }

        await BroadcastProgressAsync(progress);

        lock (_lock)
        {
            _activesyncs.Remove(syncId);
        }

        _logger.LogDebug("Completed sync {SyncId}: Success={Success}, Processed={Processed}, Failed={Failed}",
            syncId, success, progress.SuccessCount, progress.FailedCount);
    }

    private async Task BroadcastProgressAsync(SyncProgressUpdate progress)
    {
        try
        {
            _logger.LogDebug("Broadcasting progress: SyncId={SyncId}, EntityMappingId={EntityMappingId}, ConnectionId={ConnectionId}, Record {Current}/{Total}, Status={Status}",
                progress.SyncId, progress.EntityMappingId, progress.ConnectionId, progress.CurrentRecord, progress.TotalRecords, progress.Status);

            // Broadcast to sync-specific group
            await _hubContext.Clients.Group($"sync_{progress.SyncId}")
                .SendAsync("OnSyncProgress", progress);

            // Broadcast to entity mapping group for dashboard views
            if (progress.EntityMappingId > 0)
            {
                await _hubContext.Clients.Group($"entityMapping_{progress.EntityMappingId}")
                    .SendAsync("OnSyncProgress", progress);
            }

            // Broadcast to connection group for connection-level syncs (like user location sync)
            if (progress.ConnectionId.HasValue)
            {
                await _hubContext.Clients.Group($"connection_{progress.ConnectionId}")
                    .SendAsync("OnSyncProgress", progress);
            }

            _logger.LogDebug("Broadcast complete for {SyncId}", progress.SyncId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast sync progress for {SyncId}", progress.SyncId);
        }
    }
}
