using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace IDP.Controllers
{
    /// <summary>
    /// MCP (Model Context Protocol) Controller for Claude Desktop integration.
    /// Handles MCP protocol and proxies content management calls to the API.
    /// This allows everything to run on the IDP port (single ngrok tunnel).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class McpController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<McpController> _logger;

        // Session storage for active MCP sessions
        private static readonly ConcurrentDictionary<string, DateTime> _sessions = new();

        public McpController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<McpController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// MCP Streamable HTTP endpoint - handles POST (JSON-RPC).
        /// URL to use in Claude Desktop: https://your-ngrok-url/api/Mcp/mcp
        /// </summary>
        [HttpPost]
        [Route("mcp")]
        public async Task<IActionResult> McpEndpoint([FromBody] McpRequest request)
        {
            var acceptHeader = Request.Headers.Accept.ToString();
            var sessionId = Request.Headers["Mcp-Session-Id"].FirstOrDefault();
            var authHeader = Request.Headers.Authorization.FirstOrDefault();

            _logger.LogInformation("MCP request: {Method}, Session: {SessionId}", request.Method, sessionId);

            // Process the request
            var response = await ProcessMcpRequest(request, authHeader);

            // For initialize requests, create a new session and return session ID
            if (request.Method == "initialize")
            {
                var newSessionId = Guid.NewGuid().ToString();
                _sessions[newSessionId] = DateTime.UtcNow;
                Response.Headers["Mcp-Session-Id"] = newSessionId;
            }

            // Check if client accepts SSE
            if (acceptHeader.Contains("text/event-stream"))
            {
                Response.ContentType = "text/event-stream";
                Response.Headers.CacheControl = "no-cache";
                Response.Headers.Connection = "keep-alive";

                var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                var sseMessage = $"data: {json}\n\n";
                var bytes = Encoding.UTF8.GetBytes(sseMessage);
                await Response.Body.WriteAsync(bytes);
                await Response.Body.FlushAsync();
                return new EmptyResult();
            }

            // Default to JSON response
            return Ok(response);
        }

        /// <summary>
        /// GET endpoint for SSE stream (backwards compatibility).
        /// </summary>
        [HttpGet]
        [Route("mcp")]
        public async Task McpSseStream(CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            var sessionId = Guid.NewGuid().ToString();
            _sessions[sessionId] = DateTime.UtcNow;

            var endpointEvent = new { endpoint = "/api/Mcp/mcp" };
            var json = JsonSerializer.Serialize(endpointEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var endpointMessage = $"event: endpoint\ndata: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(endpointMessage);
            await Response.Body.WriteAsync(bytes, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            // Keep connection alive
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(30000, cancellationToken);
                    var pingBytes = Encoding.UTF8.GetBytes(": ping\n\n");
                    await Response.Body.WriteAsync(pingBytes, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (TaskCanceledException) { }
        }

        private async Task<McpResponse> ProcessMcpRequest(McpRequest request, string? authHeader)
        {
            try
            {
                object? result = request.Method switch
                {
                    "initialize" => HandleInitialize(),
                    "tools/list" => HandleToolsList(),
                    "tools/call" => await HandleToolCall(request, authHeader),
                    "notifications/initialized" => null,
                    _ => throw new NotSupportedException($"Method not supported: {request.Method}")
                };

                if (request.Method.StartsWith("notifications/"))
                {
                    return new McpResponse { Jsonrpc = "2.0", Id = request.Id };
                }

                return new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = request.Id,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MCP request: {Method}", request.Method);
                return new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = request.Id,
                    Error = new McpError { Code = -32603, Message = ex.Message }
                };
            }
        }

        private object HandleInitialize()
        {
            return new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name = "authscape-cms",
                    version = "1.0.0"
                }
            };
        }

        private object HandleToolsList()
        {
            var tools = new[]
            {
                new McpTool
                {
                    Name = "list_pages",
                    Description = "List all pages in the CMS with optional search filtering",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            search = new { type = "string", description = "Search term for page titles" },
                            limit = new { type = "number", description = "Max pages to return (default 20)" }
                        }
                    }
                },
                new McpTool
                {
                    Name = "get_page",
                    Description = "Get a specific page with its full content by ID",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            pageId = new { type = "string", description = "The page GUID" }
                        },
                        required = new[] { "pageId" }
                    }
                },
                new McpTool
                {
                    Name = "create_page",
                    Description = "Create a new page in the CMS",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "Page title" },
                            slug = new { type = "string", description = "URL slug" },
                            description = new { type = "string", description = "Page description" }
                        },
                        required = new[] { "title", "slug" }
                    }
                },
                new McpTool
                {
                    Name = "update_page_content",
                    Description = "Update the visual content/components of a page. Triggers real-time preview via SignalR.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            pageId = new { type = "string", description = "The page GUID" },
                            content = new
                            {
                                type = "array",
                                description = "Array of components with type and props"
                            }
                        },
                        required = new[] { "pageId", "content" }
                    }
                },
                new McpTool
                {
                    Name = "add_component",
                    Description = "Add a single component to a page. Use this for real-time building.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            pageId = new { type = "string", description = "The page GUID" },
                            component = new { type = "object", description = "Component object with type and props" },
                            position = new { type = "number", description = "Insert position (default: end)" }
                        },
                        required = new[] { "pageId", "component" }
                    }
                },
                new McpTool
                {
                    Name = "start_building",
                    Description = "Signal that you're starting to build a page. Shows a progress indicator.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            pageId = new { type = "string", description = "The page GUID" },
                            message = new { type = "string", description = "Message to show user" }
                        },
                        required = new[] { "pageId" }
                    }
                },
                new McpTool
                {
                    Name = "finish_building",
                    Description = "Signal that you've finished building the page.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            pageId = new { type = "string", description = "The page GUID" }
                        },
                        required = new[] { "pageId" }
                    }
                },
                new McpTool
                {
                    Name = "list_components",
                    Description = "List all available visual components you can use to build pages",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            category = new { type = "string", description = "Filter by category (heroes, layout, cards, etc.)" }
                        }
                    }
                },
                new McpTool
                {
                    Name = "get_component_schema",
                    Description = "Get the detailed schema for a specific component",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            componentName = new { type = "string", description = "Component name (e.g., HeroBasic)" }
                        },
                        required = new[] { "componentName" }
                    }
                },
                new McpTool
                {
                    Name = "delete_page",
                    Description = "Delete a page from the CMS",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            pageId = new { type = "string", description = "The page GUID" }
                        },
                        required = new[] { "pageId" }
                    }
                }
            };

            return new { tools };
        }

        private async Task<object> HandleToolCall(McpRequest request, string? authHeader)
        {
            var toolName = request.Params?.GetProperty("name").GetString();
            var arguments = request.Params?.GetProperty("arguments");

            // For component schema tools, handle locally
            if (toolName == "list_components")
            {
                var category = arguments?.TryGetProperty("category", out var c) == true ? c.GetString() : null;
                var result = ComponentSchemas.ListComponents(category);
                return new
                {
                    content = new[]
                    {
                        new { type = "text", text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) }
                    }
                };
            }

            if (toolName == "get_component_schema")
            {
                var componentName = arguments?.GetProperty("componentName").GetString();
                var schema = ComponentSchemas.GetSchema(componentName!);
                return new
                {
                    content = new[]
                    {
                        new { type = "text", text = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true }) }
                    }
                };
            }

            // For other tools, proxy to the API
            // Note: In production, configure the API URL properly
            var apiBaseUrl = "https://localhost:44369"; // TODO: Get from config

            var client = _httpClientFactory.CreateClient();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);
            }

            return toolName switch
            {
                "list_pages" => await ProxyListPages(client, apiBaseUrl, arguments),
                "get_page" => await ProxyGetPage(client, apiBaseUrl, arguments),
                "create_page" => await ProxyCreatePage(client, apiBaseUrl, arguments),
                "update_page_content" => await ProxyUpdatePageContent(client, apiBaseUrl, arguments),
                "add_component" => await ProxyAddComponent(client, apiBaseUrl, arguments),
                "start_building" => await ProxyStartBuilding(client, apiBaseUrl, arguments),
                "finish_building" => await ProxyFinishBuilding(client, apiBaseUrl, arguments),
                "delete_page" => await ProxyDeletePage(client, apiBaseUrl, arguments),
                _ => throw new NotSupportedException($"Unknown tool: {toolName}")
            };
        }

        #region API Proxy Methods

        private async Task<object> ProxyListPages(HttpClient client, string apiBaseUrl, JsonElement? arguments)
        {
            var search = arguments?.TryGetProperty("search", out var s) == true ? s.GetString() : "";
            var limit = arguments?.TryGetProperty("limit", out var l) == true ? l.GetInt32() : 20;

            var response = await client.PostAsJsonAsync($"{apiBaseUrl}/api/AuthscapeMCP/ListPages", new { search, limit });
            var result = await response.Content.ReadAsStringAsync();

            return new { content = new[] { new { type = "text", text = result } } };
        }

        private async Task<object> ProxyGetPage(HttpClient client, string apiBaseUrl, JsonElement? arguments)
        {
            var pageId = arguments?.GetProperty("pageId").GetString();
            var response = await client.GetAsync($"{apiBaseUrl}/api/AuthscapeMCP/GetPage?pageId={pageId}");
            var result = await response.Content.ReadAsStringAsync();

            return new { content = new[] { new { type = "text", text = result } } };
        }

        private async Task<object> ProxyCreatePage(HttpClient client, string apiBaseUrl, JsonElement? arguments)
        {
            var title = arguments?.GetProperty("title").GetString() ?? "";
            var slug = arguments?.GetProperty("slug").GetString() ?? "";
            var description = arguments?.TryGetProperty("description", out var d) == true ? d.GetString() : "";

            var response = await client.PostAsJsonAsync($"{apiBaseUrl}/api/AuthscapeMCP/CreatePage", new { title, slug, description });
            var result = await response.Content.ReadAsStringAsync();

            return new { content = new[] { new { type = "text", text = result } } };
        }

        private async Task<object> ProxyUpdatePageContent(HttpClient client, string apiBaseUrl, JsonElement? arguments)
        {
            var pageId = arguments?.GetProperty("pageId").GetString();
            var content = arguments?.GetProperty("content");

            var response = await client.PostAsJsonAsync($"{apiBaseUrl}/api/AuthscapeMCP/UpdatePageContent", new { pageId, content });
            var result = await response.Content.ReadAsStringAsync();

            return new { content = new[] { new { type = "text", text = result } } };
        }

        private async Task<object> ProxyAddComponent(HttpClient client, string apiBaseUrl, JsonElement? arguments)
        {
            var pageId = arguments?.GetProperty("pageId").GetString();
            var component = arguments?.GetProperty("component");
            var position = arguments?.TryGetProperty("position", out var p) == true ? p.GetInt32() : (int?)null;

            var response = await client.PostAsJsonAsync($"{apiBaseUrl}/api/AuthscapeMCP/AddComponent", new { pageId, component, position });
            var result = await response.Content.ReadAsStringAsync();

            return new { content = new[] { new { type = "text", text = result } } };
        }

        private async Task<object> ProxyStartBuilding(HttpClient client, string apiBaseUrl, JsonElement? arguments)
        {
            var pageId = arguments?.GetProperty("pageId").GetString();
            var message = arguments?.TryGetProperty("message", out var m) == true ? m.GetString() : "Building page...";

            var response = await client.PostAsJsonAsync($"{apiBaseUrl}/api/AuthscapeMCP/StartBuilding", new { pageId, message });
            var result = await response.Content.ReadAsStringAsync();

            return new { content = new[] { new { type = "text", text = result } } };
        }

        private async Task<object> ProxyFinishBuilding(HttpClient client, string apiBaseUrl, JsonElement? arguments)
        {
            var pageId = arguments?.GetProperty("pageId").GetString();

            var response = await client.PostAsJsonAsync($"{apiBaseUrl}/api/AuthscapeMCP/FinishBuilding", new { pageId });
            var result = await response.Content.ReadAsStringAsync();

            return new { content = new[] { new { type = "text", text = result } } };
        }

        private async Task<object> ProxyDeletePage(HttpClient client, string apiBaseUrl, JsonElement? arguments)
        {
            var pageId = arguments?.GetProperty("pageId").GetString();

            var response = await client.PostAsync($"{apiBaseUrl}/api/AuthscapeMCP/DeletePage?pageId={pageId}", null);
            var result = await response.Content.ReadAsStringAsync();

            return new { content = new[] { new { type = "text", text = result } } };
        }

        #endregion
    }

    #region MCP Protocol Models

    public class McpRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = "";

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }

    public class McpResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("result")]
        public object? Result { get; set; }

        [JsonPropertyName("error")]
        public McpError? Error { get; set; }
    }

    public class McpError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public class McpTool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("inputSchema")]
        public object InputSchema { get; set; } = new { };
    }

    #endregion

    #region Component Schemas (Local - no API call needed)

    public static class ComponentSchemas
    {
        private static readonly Dictionary<string, ComponentCategory> Categories = new()
        {
            ["heroes"] = new() { Title = "Heroes", Components = new[] { "HeroBasic", "HeroSplit", "HeroVideo", "HeroSlider", "HeroImage", "HeroAnimated" } },
            ["layout"] = new() { Title = "Layout", Components = new[] { "Section", "Row", "Spacer", "Grid", "Stack", "FlexBox" } },
            ["typography"] = new() { Title = "Typography", Components = new[] { "Heading", "Paragraph", "RichText" } },
            ["cards"] = new() { Title = "Cards", Components = new[] { "Card", "FeatureCard", "PricingCard", "TestimonialCard", "TeamCard", "BlogCard", "ProductCard", "ServiceCard", "PortfolioCard", "ComparisonCard" } },
            ["media"] = new() { Title = "Media", Components = new[] { "Video", "Gallery", "Carousel", "ImageCompare", "Icon", "Avatar", "AudioPlayer", "LottieAnimation" } },
            ["cta"] = new() { Title = "Call to Action", Components = new[] { "CTABanner", "Newsletter", "AnnouncementBar", "FloatingCTA", "CTACard" } },
            ["social"] = new() { Title = "Social & Trust", Components = new[] { "SocialLinks", "ShareButtons", "TrustBadges", "ReviewStars", "ClientLogos", "TestimonialSlider" } },
            ["navigation"] = new() { Title = "Navigation", Components = new[] { "Accordion", "Tabs", "Breadcrumb", "StepIndicator", "ScrollToTop", "TableOfContents" } },
            ["data"] = new() { Title = "Data Display", Components = new[] { "Stats", "Timeline", "ProgressBar", "CircularProgress", "Countdown", "Counter", "Metric", "SkillBars" } },
            ["interactive"] = new() { Title = "Interactive", Components = new[] { "Modal", "Tooltip", "Popover", "Alert", "Toast", "Drawer" } },
            ["forms"] = new() { Title = "Forms", Components = new[] { "FormInput", "FormTextArea", "FormSelect", "FormCheckbox", "FormRadioGroup", "FormDatePicker", "FormFileUpload", "FormBuilder" } },
            ["content"] = new() { Title = "Content Blocks", Components = new[] { "Quote", "List", "Badge", "Highlight", "FAQ", "Features", "Steps", "ContentBlock", "Testimonials", "PricingTable" } },
            ["ecommerce"] = new() { Title = "E-Commerce", Components = new[] { "MarketplaceEmbed", "ShoppingCart" } },
            ["maps"] = new() { Title = "Maps & Location", Components = new[] { "MapEmbed", "ContactInfo", "LocationCard" } },
            ["utility"] = new() { Title = "Utility", Components = new[] { "HTMLEmbed", "IFrame", "CodeBlock", "Anchor" } }
        };

        private static readonly Dictionary<string, ComponentSchema> Schemas = new()
        {
            ["HeroBasic"] = new() { Name = "HeroBasic", Description = "A basic hero section with title, subtitle, and CTA button", Category = "heroes", Fields = new() { ["title"] = new() { Type = "text", Description = "Main headline text" }, ["subtitle"] = new() { Type = "textarea", Description = "Supporting text" }, ["ctaText"] = new() { Type = "text", Description = "Button text" }, ["ctaLink"] = new() { Type = "text", Description = "Button URL" }, ["backgroundColor"] = new() { Type = "text" }, ["textAlign"] = new() { Type = "select", Options = new[] { "left", "center", "right" } } } },
            ["HeroSplit"] = new() { Name = "HeroSplit", Description = "Hero with content on one side and image on the other", Category = "heroes", Fields = new() { ["title"] = new() { Type = "text" }, ["subtitle"] = new() { Type = "textarea" }, ["image"] = new() { Type = "text" }, ["imagePosition"] = new() { Type = "select", Options = new[] { "left", "right" } } } },
            ["Section"] = new() { Name = "Section", Description = "A container section with padding and background options", Category = "layout", Fields = new() { ["backgroundColor"] = new() { Type = "text" }, ["padding"] = new() { Type = "number" }, ["maxWidth"] = new() { Type = "select", Options = new[] { "sm", "md", "lg", "xl", "full" } } } },
            ["Row"] = new() { Name = "Row", Description = "Horizontal row layout for columns", Category = "layout", Fields = new() { ["columns"] = new() { Type = "number" }, ["gap"] = new() { Type = "number" } } },
            ["Heading"] = new() { Name = "Heading", Description = "Text heading (h1-h6)", Category = "typography", Fields = new() { ["text"] = new() { Type = "text" }, ["level"] = new() { Type = "select", Options = new[] { "h1", "h2", "h3", "h4", "h5", "h6" } } } },
            ["Paragraph"] = new() { Name = "Paragraph", Description = "Body text paragraph", Category = "typography", Fields = new() { ["text"] = new() { Type = "textarea" }, ["align"] = new() { Type = "select", Options = new[] { "left", "center", "right" } } } },
            ["Card"] = new() { Name = "Card", Description = "Basic card component", Category = "cards", Fields = new() { ["title"] = new() { Type = "text" }, ["description"] = new() { Type = "textarea" }, ["image"] = new() { Type = "text" } } },
            ["FeatureCard"] = new() { Name = "FeatureCard", Description = "Card highlighting a feature with icon", Category = "cards", Fields = new() { ["title"] = new() { Type = "text" }, ["description"] = new() { Type = "textarea" }, ["icon"] = new() { Type = "text" } } },
            ["PricingCard"] = new() { Name = "PricingCard", Description = "Pricing plan card", Category = "cards", Fields = new() { ["planName"] = new() { Type = "text" }, ["price"] = new() { Type = "text" }, ["features"] = new() { Type = "textarea" }, ["ctaText"] = new() { Type = "text" } } },
            ["CTABanner"] = new() { Name = "CTABanner", Description = "Call-to-action banner", Category = "cta", Fields = new() { ["title"] = new() { Type = "text" }, ["subtitle"] = new() { Type = "textarea" }, ["ctaText"] = new() { Type = "text" }, ["ctaLink"] = new() { Type = "text" } } },
            ["Features"] = new() { Name = "Features", Description = "Feature list/grid", Category = "content", Fields = new() { ["features"] = new() { Type = "array", Description = "Array of {title, description, icon} objects" }, ["columns"] = new() { Type = "number" } } },
            ["FAQ"] = new() { Name = "FAQ", Description = "Frequently asked questions accordion", Category = "content", Fields = new() { ["items"] = new() { Type = "array", Description = "Array of {question, answer} objects" } } },
            ["Testimonials"] = new() { Name = "Testimonials", Description = "Testimonial display", Category = "content", Fields = new() { ["testimonials"] = new() { Type = "array" }, ["layout"] = new() { Type = "select", Options = new[] { "grid", "slider" } } } },
            // Add more as needed - keeping it shorter for now
        };

        public static object ListComponents(string? category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return new
                {
                    categories = Categories.Select(c => new
                    {
                        key = c.Key,
                        title = c.Value.Title,
                        components = c.Value.Components
                    }),
                    totalComponents = 100
                };
            }

            if (Categories.TryGetValue(category.ToLower(), out var cat))
            {
                return new
                {
                    category = cat.Title,
                    components = cat.Components.Select(name => new { name, description = Schemas.TryGetValue(name, out var s) ? s.Description : "" })
                };
            }

            return new { error = $"Unknown category: {category}" };
        }

        public static object GetSchema(string componentName)
        {
            if (Schemas.TryGetValue(componentName, out var schema))
            {
                return new
                {
                    name = schema.Name,
                    description = schema.Description,
                    category = schema.Category,
                    fields = schema.Fields.Select(f => new { name = f.Key, type = f.Value.Type, description = f.Value.Description, options = f.Value.Options })
                };
            }

            return new { error = $"Unknown component: {componentName}. Use list_components to see available components." };
        }

        private class ComponentCategory
        {
            public string Title { get; set; } = "";
            public string[] Components { get; set; } = Array.Empty<string>();
        }

        private class ComponentSchema
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Category { get; set; } = "";
            public Dictionary<string, FieldSchema> Fields { get; set; } = new();
        }

        private class FieldSchema
        {
            public string Type { get; set; } = "";
            public string? Description { get; set; }
            public string[]? Options { get; set; }
        }
    }

    #endregion
}
