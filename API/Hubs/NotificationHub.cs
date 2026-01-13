using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;

namespace AuthScape.Core.Hubs
{
    // TODO: Re-enable [Authorize] after testing
    // [Authorize]
    public class NotificationHub : Hub
    {
        public async Task JoinUserNotifications(long userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        public async Task JoinCompanyNotifications(long companyId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"company_{companyId}");
        }

        public async Task JoinLocationNotifications(long locationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"location_{locationId}");
        }

        public async Task LeaveUserNotifications(long userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        public async Task LeaveCompanyNotifications(long companyId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"company_{companyId}");
        }

        public async Task LeaveLocationNotifications(long locationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"location_{locationId}");
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            // Cleanup happens automatically when client disconnects
            await base.OnDisconnectedAsync(exception);
        }
    }
}
