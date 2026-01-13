using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace AuthScape.ErrorTracking.Hubs
{
    /// <summary>
    /// SignalR hub for real-time error tracking updates.
    /// Clients can subscribe to receive notifications when new errors are logged.
    /// </summary>
    public class ErrorTrackingHub : Hub
    {
        /// <summary>
        /// Join the error tracking channel to receive real-time error updates.
        /// </summary>
        public async Task JoinErrorTracking()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "error_tracking");
        }

        /// <summary>
        /// Leave the error tracking channel.
        /// </summary>
        public async Task LeaveErrorTracking()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "error_tracking");
        }

        /// <summary>
        /// Join a specific error group to receive updates for that error.
        /// </summary>
        public async Task JoinErrorGroup(Guid errorGroupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"error_group_{errorGroupId}");
        }

        /// <summary>
        /// Leave a specific error group.
        /// </summary>
        public async Task LeaveErrorGroup(Guid errorGroupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"error_group_{errorGroupId}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
