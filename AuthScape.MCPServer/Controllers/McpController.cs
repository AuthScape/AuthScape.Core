using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthScape.MCPServer.Controllers;

/// <summary>
/// MCP (Model Context Protocol) endpoint for Claude Desktop.
/// Implements the MCP Streamable HTTP transport (2025-03-26 spec).
/// </summary>
[Route("mcp")]
[ApiController]
public class McpController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<McpController> _logger;
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
    /// Main MCP endpoint - handles JSON-RPC requests from Claude Desktop.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> HandleMcpRequest([FromBody] JsonElement request)
    {
        var sessionId = Request.Headers["Mcp-Session-Id"].FirstOrDefault();

        _logger.LogInformation("MCP request received. SessionId: {SessionId}, Body: {Body}",
            sessionId, request.GetRawText());

        try
        {
            var method = request.GetProperty("method").GetString();
            var id = request.TryGetProperty("id", out var idElement) ? idElement : (JsonElement?)null;

            _logger.LogInformation("MCP method: {Method}", method);

            object? result = method switch
            {
                "initialize" => HandleInitialize(request, sessionId),
                "notifications/initialized" => null, // No response needed for notifications
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolCall(request),
                "resources/list" => HandleResourcesList(),
                "prompts/list" => HandlePromptsList(),
                _ => throw new McpException(-32601, $"Method not found: {method}")
            };

            // Notifications don't get responses
            if (method?.StartsWith("notifications/") == true)
            {
                return Ok();
            }

            var response = new
            {
                jsonrpc = "2.0",
                id = id,
                result = result
            };

            // Set session header if this is initialize
            if (method == "initialize" && !string.IsNullOrEmpty(sessionId))
            {
                Response.Headers.Append("Mcp-Session-Id", sessionId);
            }

            return Ok(response);
        }
        catch (McpException ex)
        {
            _logger.LogWarning("MCP error: {Code} - {Message}", ex.Code, ex.Message);

            var id = request.TryGetProperty("id", out var idElement) ? idElement : (JsonElement?)null;
            return Ok(new
            {
                jsonrpc = "2.0",
                id = id,
                error = new { code = ex.Code, message = ex.Message }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling MCP request");

            var id = request.TryGetProperty("id", out var idElement) ? idElement : (JsonElement?)null;
            return Ok(new
            {
                jsonrpc = "2.0",
                id = id,
                error = new { code = -32603, message = "Internal error" }
            });
        }
    }

    /// <summary>
    /// GET endpoint for SSE streaming (optional, for future use).
    /// </summary>
    [HttpGet]
    public IActionResult GetMcpInfo()
    {
        return Ok(new
        {
            name = "AuthScape MCP Server",
            version = "1.0.0",
            description = "MCP server for AuthScape CMS - manage web pages with Claude"
        });
    }

    private object HandleInitialize(JsonElement request, string? sessionId)
    {
        // Generate session ID if not provided
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
        }

        _sessions[sessionId] = DateTime.UtcNow;

        var clientInfo = request.TryGetProperty("params", out var paramsEl)
            && paramsEl.TryGetProperty("clientInfo", out var clientInfoEl)
            ? clientInfoEl
            : (JsonElement?)null;

        _logger.LogInformation("MCP initialized. Session: {SessionId}, Client: {ClientInfo}",
            sessionId, clientInfo?.GetRawText() ?? "unknown");

        return new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { listChanged = false },
                resources = new { subscribe = false, listChanged = false },
                prompts = new { listChanged = false }
            },
            serverInfo = new
            {
                name = "AuthScape CMS",
                version = "1.0.0"
            }
        };
    }

    private object HandleToolsList()
    {
        var tools = new List<object>
        {
            new Dictionary<string, object>
            {
                ["name"] = "list_pages",
                ["description"] = "List all CMS pages in the system",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>(),
                    ["required"] = Array.Empty<string>()
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "get_page",
                ["description"] = "Get details of a specific CMS page by ID",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["pageId"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["description"] = "The ID of the page to retrieve"
                        }
                    },
                    ["required"] = new[] { "pageId" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "create_page",
                ["description"] = "Create a new CMS page",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["title"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "The title of the page"
                        },
                        ["slug"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "The URL slug for the page"
                        },
                        ["content"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "The HTML content of the page"
                        }
                    },
                    ["required"] = new[] { "title", "slug" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "update_page",
                ["description"] = "Update an existing CMS page",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["pageId"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["description"] = "The ID of the page to update"
                        },
                        ["title"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "The new title of the page"
                        },
                        ["content"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "The new HTML content of the page"
                        }
                    },
                    ["required"] = new[] { "pageId" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "delete_page",
                ["description"] = "Delete a CMS page",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["pageId"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["description"] = "The ID of the page to delete"
                        }
                    },
                    ["required"] = new[] { "pageId" }
                }
            }
        };

        return new { tools };
    }

    private async Task<object> HandleToolCall(JsonElement request)
    {
        var paramsEl = request.GetProperty("params");
        var toolName = paramsEl.GetProperty("name").GetString();
        var arguments = paramsEl.TryGetProperty("arguments", out var argsEl) ? argsEl : (JsonElement?)null;

        _logger.LogInformation("Tool call: {ToolName}, Arguments: {Arguments}",
            toolName, arguments?.GetRawText() ?? "{}");

        // Get the API URL to proxy to
        var apiUrl = _configuration.GetSection("AppSettings:ApiUrl").Value
            ?? _configuration["ApiUrl"]
            ?? "https://localhost:44369";

        try
        {
            var result = toolName switch
            {
                "list_pages" => await CallApiAsync(apiUrl, "GET", "/api/pages"),
                "get_page" => await CallApiAsync(apiUrl, "GET", $"/api/pages/{arguments?.GetProperty("pageId").GetInt32()}"),
                "create_page" => await CallApiAsync(apiUrl, "POST", "/api/pages", arguments),
                "update_page" => await CallApiAsync(apiUrl, "PUT", $"/api/pages/{arguments?.GetProperty("pageId").GetInt32()}", arguments),
                "delete_page" => await CallApiAsync(apiUrl, "DELETE", $"/api/pages/{arguments?.GetProperty("pageId").GetInt32()}"),
                _ => throw new McpException(-32602, $"Unknown tool: {toolName}")
            };

            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = result
                    }
                }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("API call failed: {Message}", ex.Message);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error calling API: {ex.Message}"
                    }
                },
                isError = true
            };
        }
    }

    private async Task<string> CallApiAsync(string baseUrl, string method, string path, JsonElement? body = null)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);

        // Get access token from request if present
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader))
        {
            client.DefaultRequestHeaders.Add("Authorization", authHeader);
        }

        HttpResponseMessage response;

        switch (method.ToUpper())
        {
            case "GET":
                response = await client.GetAsync(path);
                break;
            case "POST":
                response = await client.PostAsJsonAsync(path, body);
                break;
            case "PUT":
                response = await client.PutAsJsonAsync(path, body);
                break;
            case "DELETE":
                response = await client.DeleteAsync(path);
                break;
            default:
                throw new McpException(-32602, $"Unsupported HTTP method: {method}");
        }

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("API returned {StatusCode}: {Content}", response.StatusCode, content);
            return $"API returned {response.StatusCode}: {content}";
        }

        return content;
    }

    private object HandleResourcesList()
    {
        return new { resources = Array.Empty<object>() };
    }

    private object HandlePromptsList()
    {
        return new { prompts = Array.Empty<object>() };
    }
}

public class McpException : Exception
{
    public int Code { get; }

    public McpException(int code, string message) : base(message)
    {
        Code = code;
    }
}
