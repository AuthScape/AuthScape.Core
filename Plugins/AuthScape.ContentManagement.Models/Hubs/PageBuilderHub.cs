using Microsoft.AspNetCore.SignalR;

namespace AuthScape.ContentManagement.Models.Hubs
{
    /// <summary>
    /// SignalR hub for real-time page building with Claude MCP.
    /// Allows clients to receive live updates as components are added to a page.
    /// </summary>
    public class PageBuilderHub : Hub
    {
        /// <summary>
        /// Join a page editing session to receive real-time updates.
        /// </summary>
        /// <param name="pageId">The GUID of the page being edited</param>
        public async Task JoinPage(Guid pageId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, pageId.ToString());
        }

        /// <summary>
        /// Leave a page editing session.
        /// </summary>
        /// <param name="pageId">The GUID of the page</param>
        public async Task LeavePage(Guid pageId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, pageId.ToString());
        }

        /// <summary>
        /// Broadcast that a single component has been added to a page.
        /// </summary>
        /// <param name="pageId">The page GUID</param>
        /// <param name="component">The component object (type and props)</param>
        /// <param name="index">The index where the component was inserted</param>
        public async Task ComponentAdded(Guid pageId, object component, int index)
        {
            await Clients.Group(pageId.ToString())
                .SendAsync("OnComponentAdded", component, index);
        }

        /// <summary>
        /// Broadcast that a component has been updated.
        /// </summary>
        /// <param name="pageId">The page GUID</param>
        /// <param name="index">The index of the updated component</param>
        /// <param name="component">The updated component object</param>
        public async Task ComponentUpdated(Guid pageId, int index, object component)
        {
            await Clients.Group(pageId.ToString())
                .SendAsync("OnComponentUpdated", index, component);
        }

        /// <summary>
        /// Broadcast that a component has been removed.
        /// </summary>
        /// <param name="pageId">The page GUID</param>
        /// <param name="index">The index of the removed component</param>
        public async Task ComponentRemoved(Guid pageId, int index)
        {
            await Clients.Group(pageId.ToString())
                .SendAsync("OnComponentRemoved", index);
        }

        /// <summary>
        /// Broadcast that the entire page content has been replaced.
        /// </summary>
        /// <param name="pageId">The page GUID</param>
        /// <param name="content">The full Puck content object</param>
        public async Task ContentReplaced(Guid pageId, object content)
        {
            await Clients.Group(pageId.ToString())
                .SendAsync("OnContentReplaced", content);
        }

        /// <summary>
        /// Signal that Claude has started building a page.
        /// </summary>
        /// <param name="pageId">The page GUID</param>
        /// <param name="message">Optional status message (e.g., "Building your landing page...")</param>
        public async Task BuildingStarted(Guid pageId, string? message = null)
        {
            await Clients.Group(pageId.ToString())
                .SendAsync("OnBuildingStarted", message ?? "Building page...");
        }

        /// <summary>
        /// Signal that Claude has finished building a page.
        /// </summary>
        /// <param name="pageId">The page GUID</param>
        public async Task BuildingCompleted(Guid pageId)
        {
            await Clients.Group(pageId.ToString())
                .SendAsync("OnBuildingCompleted");
        }

        /// <summary>
        /// Send a progress update during page building.
        /// </summary>
        /// <param name="pageId">The page GUID</param>
        /// <param name="message">Progress message</param>
        /// <param name="currentStep">Current step number</param>
        /// <param name="totalSteps">Total number of steps (optional)</param>
        public async Task BuildingProgress(Guid pageId, string message, int currentStep, int? totalSteps = null)
        {
            await Clients.Group(pageId.ToString())
                .SendAsync("OnBuildingProgress", message, currentStep, totalSteps);
        }
    }
}
