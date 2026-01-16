using Microsoft.AspNetCore.SignalR;

namespace AuthScape.CRM.Hubs;

/// <summary>
/// SignalR hub for real-time CRM sync progress updates
/// </summary>
public class CrmSyncHub : Hub
{
    /// <summary>
    /// Join a group to receive progress updates for a specific sync operation or entity mapping
    /// Pass a syncId like "abc123" or an entity mapping group like "entityMapping_5"
    /// </summary>
    public async Task JoinSyncProgress(string groupName)
    {
        // If it already contains an underscore, it's a fully qualified group name
        // Otherwise, prepend "sync_" for backward compatibility with syncIds
        var finalGroupName = groupName.Contains('_') ? groupName : $"sync_{groupName}";
        await Groups.AddToGroupAsync(Context.ConnectionId, finalGroupName);
    }

    /// <summary>
    /// Leave a sync progress group
    /// </summary>
    public async Task LeaveSyncProgress(string groupName)
    {
        var finalGroupName = groupName.Contains('_') ? groupName : $"sync_{groupName}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, finalGroupName);
    }

    /// <summary>
    /// Join a group to receive progress updates for a connection
    /// </summary>
    public async Task JoinConnectionProgress(long connectionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"connection_{connectionId}");
    }

    /// <summary>
    /// Leave a connection progress group
    /// </summary>
    public async Task LeaveConnectionProgress(long connectionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"connection_{connectionId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Progress update payload sent to clients
/// </summary>
public class SyncProgressUpdate
{
    public string SyncId { get; set; } = string.Empty;
    public long EntityMappingId { get; set; }
    public long? ConnectionId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public int CurrentRecord { get; set; }
    public int TotalRecords { get; set; }
    public int PercentComplete { get; set; }
    public string Status { get; set; } = "InProgress"; // InProgress, Completed, Failed
    public string? CurrentOperation { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public string? ErrorMessage { get; set; }
}
