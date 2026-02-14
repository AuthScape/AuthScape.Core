using AuthScape.AI;
using AuthScape.ContentManagement.Models.Hubs;
using AuthScape.ContentManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Text.RegularExpressions;
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
        private readonly IAIService _aiService;

        public AIDesignerController(
            IContentManagementService contentManagementService,
            IHubContext<PageBuilderHub> hubContext,
            IAIService aiService)
        {
            _contentManagementService = contentManagementService;
            _hubContext = hubContext;
            _aiService = aiService;
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

        /// <summary>
        /// Generate page content from a user prompt using the Claude CLI.
        /// Broadcasts building status and content updates via SignalR.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GenerateFromPrompt([FromBody] AIPromptRequest request)
        {
            if (request.PageId == Guid.Empty)
                return BadRequest(new { error = "PageId is required" });

            if (string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest(new { error = "Prompt is required" });

            var pageGroup = request.PageId.ToString();

            try
            {
                // Notify clients that AI generation has started
                await _hubContext.Clients
                    .Group(pageGroup)
                    .SendAsync("OnBuildingStarted", "AI is generating your design...");

                // Build the full prompt with system instructions
                var contextSection = "";
                if (!string.IsNullOrWhiteSpace(request.CurrentHtml))
                {
                    contextSection = $@"

The page currently contains this HTML content (modify or build upon it as appropriate):
```html
{request.CurrentHtml}
```";
                    if (!string.IsNullOrWhiteSpace(request.CurrentCss))
                    {
                        contextSection += $@"

Current CSS:
```css
{request.CurrentCss}
```";
                    }
                }

                var fullPrompt = $@"You are an expert web page designer. Generate clean, modern, responsive HTML and CSS based on the user's request.

Rules:
- Return the complete page HTML inside a ```html code fence
- Return the CSS inside a ```css code fence
- Use modern CSS (flexbox, grid, clamp, etc.)
- Make the design responsive and mobile-friendly
- Use professional color schemes and typography
- Include Google Fonts links in the HTML if needed
- Do not include <html>, <head>, or <body> tags — only the inner content
- Do not use JavaScript
{contextSection}

User request: {request.Prompt}";

                // Call Claude CLI via the AI service
                _aiService.SetProvider(AuthScape.AI.Enums.AIProvider.ClaudeCli);
                var response = await _aiService.ChatAsync(fullPrompt);
                var aiText = response.Text;

                // Parse HTML and CSS from fenced code blocks
                var html = ExtractCodeBlock(aiText, "html");
                var css = ExtractCodeBlock(aiText, "css");

                // Fallback: if no html fence found, treat the entire response as HTML
                if (string.IsNullOrWhiteSpace(html))
                {
                    html = aiText;
                }

                var contentToSave = new { html, css = css ?? "" };

                // Save to database
                var contentJson = System.Text.Json.JsonSerializer.Serialize(contentToSave);
                await _contentManagementService.UpdatePageContent(request.PageId, contentJson);

                // Broadcast the content update
                await _hubContext.Clients
                    .Group(pageGroup)
                    .SendAsync("OnContentReplaced", contentToSave);

                // Notify clients that building is complete
                await _hubContext.Clients
                    .Group(pageGroup)
                    .SendAsync("OnBuildingCompleted");

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                // Always notify completion even on error
                await _hubContext.Clients
                    .Group(pageGroup)
                    .SendAsync("OnBuildingCompleted");

                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Extracts content from a fenced code block (e.g. ```html ... ```).
        /// </summary>
        private static string? ExtractCodeBlock(string text, string language)
        {
            var pattern = $@"```{language}\s*\n([\s\S]*?)```";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
    }

    #region Request Models

    /// <summary>
    /// Request model for generating page content from a user prompt.
    /// </summary>
    public class AIPromptRequest
    {
        public Guid PageId { get; set; }
        public string Prompt { get; set; } = "";
        public string? CurrentHtml { get; set; }
        public string? CurrentCss { get; set; }
    }

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
