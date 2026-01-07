using AuthScape.ContentManagement.Models.Hubs;
using AuthScape.ContentManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace API.Controllers
{
    /// <summary>
    /// Controller for AI Designer WPF application integration.
    /// Allows external apps to update page content and broadcast changes via SignalR.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AIDesignerController : ControllerBase
    {
        private readonly IContentManagementService _contentManagementService;
        private readonly IHubContext<PageBuilderHub> _hubContext;

        public AIDesignerController(
            IContentManagementService contentManagementService,
            IHubContext<PageBuilderHub> hubContext)
        {
            _contentManagementService = contentManagementService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Get all pages for the WPF app page selector dropdown.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPages(long? privateLabelCompanyId = null)
        {
            var pages = await _contentManagementService.GetPagesForAIDesigner(privateLabelCompanyId);
            return Ok(pages);
        }

        /// <summary>
        /// Get the current content of a page for context in AI generation.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPageContent(Guid pageId)
        {
            var page = await _contentManagementService.GetPage(pageId);
            if (page == null)
            {
                return NotFound(new { error = "Page not found" });
            }
            return Ok(new {
                pageId = page.Id,
                title = page.Title,
                content = page.Content
            });
        }

        /// <summary>
        /// Update page content from AI Designer and broadcast the change via SignalR.
        /// This allows the GrapeJS editor to receive real-time updates.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdatePageContent([FromBody] AIUpdateRequest request)
        {
            if (request.PageId == Guid.Empty)
            {
                return BadRequest(new { error = "PageId is required" });
            }

            try
            {
                // Build the content object based on mode
                object contentToSave;

                if (request.Mode == "replace")
                {
                    // For replace mode, we store HTML and CSS directly
                    contentToSave = new
                    {
                        html = request.Html,
                        css = request.Css ?? ""
                    };
                }
                else
                {
                    // For insert/modify modes, get existing content and merge
                    var existingPage = await _contentManagementService.GetPage(request.PageId);
                    if (existingPage == null)
                    {
                        return NotFound(new { error = "Page not found" });
                    }

                    // For now, just append for insert mode
                    // More sophisticated merging could be added later
                    var existingHtml = "";
                    if (!string.IsNullOrEmpty(existingPage.Content))
                    {
                        try
                        {
                            var existing = System.Text.Json.JsonSerializer.Deserialize<dynamic>(existingPage.Content);
                            existingHtml = existing?.GetProperty("html").GetString() ?? "";
                        }
                        catch
                        {
                            existingHtml = existingPage.Content;
                        }
                    }

                    contentToSave = new
                    {
                        html = request.Mode == "insert"
                            ? existingHtml + request.Html
                            : request.Html,
                        css = request.Css ?? ""
                    };
                }

                // Serialize and save to database
                var contentJson = System.Text.Json.JsonSerializer.Serialize(contentToSave);
                await _contentManagementService.UpdatePageContent(request.PageId, contentJson);

                // Broadcast the update to all connected clients editing this page
                await _hubContext.Clients
                    .Group(request.PageId.ToString())
                    .SendAsync("OnContentReplaced", contentToSave);

                return Ok(new { success = true, message = "Page content updated and broadcasted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Signal that AI generation has started for a page.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> NotifyBuildingStarted([FromBody] BuildingStatusRequest request)
        {
            await _hubContext.Clients
                .Group(request.PageId.ToString())
                .SendAsync("OnBuildingStarted", request.Message ?? "AI is generating your design...");

            return Ok();
        }

        /// <summary>
        /// Signal that AI generation has completed for a page.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> NotifyBuildingCompleted([FromBody] BuildingStatusRequest request)
        {
            await _hubContext.Clients
                .Group(request.PageId.ToString())
                .SendAsync("OnBuildingCompleted");

            return Ok();
        }

        /// <summary>
        /// Send a progress update during AI generation.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> NotifyBuildingProgress([FromBody] BuildingProgressRequest request)
        {
            await _hubContext.Clients
                .Group(request.PageId.ToString())
                .SendAsync("OnBuildingProgress", request.Message, request.CurrentStep, request.TotalSteps);

            return Ok();
        }
    }

    #region Request Models

    /// <summary>
    /// Request model for updating page content from AI Designer.
    /// </summary>
    public class AIUpdateRequest
    {
        /// <summary>The page to update</summary>
        public Guid PageId { get; set; }

        /// <summary>The generated HTML content</summary>
        public string Html { get; set; } = "";

        /// <summary>The generated CSS styles</summary>
        public string? Css { get; set; }

        /// <summary>Update mode: "replace", "insert", or "modify"</summary>
        public string Mode { get; set; } = "replace";

        /// <summary>For modify mode: the ID of the component to modify</summary>
        public string? SelectedComponentId { get; set; }
    }

    /// <summary>
    /// Request model for building status notifications.
    /// </summary>
    public class BuildingStatusRequest
    {
        public Guid PageId { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// Request model for building progress notifications.
    /// </summary>
    public class BuildingProgressRequest
    {
        public Guid PageId { get; set; }
        public string Message { get; set; } = "";
        public int CurrentStep { get; set; }
        public int? TotalSteps { get; set; }
    }

    #endregion
}
