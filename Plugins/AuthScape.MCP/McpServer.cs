using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthScape.MCP;

/// <summary>
/// MCP Server that communicates via stdio (stdin/stdout) using JSON-RPC protocol.
/// This is compatible with Claude Desktop's MCP implementation.
/// </summary>
public class McpServer
{
    private readonly AuthScapeApiClient _apiClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(string apiUrl, string accessToken)
    {
        _apiClient = new AuthScapeApiClient(apiUrl, accessToken);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }

    public async Task RunAsync()
    {
        // Read from stdin, write to stdout
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            try
            {
                var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
                if (request == null) continue;

                var response = await HandleRequestAsync(request);
                var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                await writer.WriteLineAsync(responseJson);
            }
            catch (Exception ex)
            {
                var errorResponse = new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = null,
                    Error = new McpError { Code = -32700, Message = ex.Message }
                };
                var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                await writer.WriteLineAsync(errorJson);
            }
        }
    }

    private async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        try
        {
            object? result = request.Method switch
            {
                "initialize" => HandleInitialize(),
                "notifications/initialized" => null, // Acknowledge, no response needed
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolCallAsync(request),
                _ => throw new NotSupportedException($"Method not supported: {request.Method}")
            };

            // Don't send response for notifications
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
        return new
        {
            tools = GetToolDefinitions()
        };
    }

    private async Task<object> HandleToolCallAsync(McpRequest request)
    {
        var paramsElement = request.Params;
        if (paramsElement == null)
            throw new ArgumentException("Missing params");

        var toolName = paramsElement.Value.GetProperty("name").GetString();
        var arguments = paramsElement.Value.TryGetProperty("arguments", out var args) ? args : (JsonElement?)null;

        var result = toolName switch
        {
            "list_pages" => await _apiClient.ListPagesAsync(arguments),
            "get_page" => await _apiClient.GetPageAsync(arguments),
            "create_page" => await _apiClient.CreatePageAsync(arguments),
            "update_page_content" => await _apiClient.UpdatePageContentAsync(arguments),
            "add_component" => await _apiClient.AddComponentAsync(arguments),
            "start_building" => await _apiClient.StartBuildingAsync(arguments),
            "finish_building" => await _apiClient.FinishBuildingAsync(arguments),
            "list_components" => _apiClient.ListComponents(arguments),
            "get_component_schema" => _apiClient.GetComponentSchema(arguments),
            "delete_page" => await _apiClient.DeletePageAsync(arguments),
            _ => throw new NotSupportedException($"Unknown tool: {toolName}")
        };

        return new
        {
            content = new[]
            {
                new { type = "text", text = result }
            }
        };
    }

    private object[] GetToolDefinitions()
    {
        return new object[]
        {
            new
            {
                name = "list_pages",
                description = "List all pages in the AuthScape CMS with optional search filtering",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        search = new { type = "string", description = "Search term for page titles" },
                        limit = new { type = "number", description = "Max pages to return (default 20)" }
                    }
                }
            },
            new
            {
                name = "get_page",
                description = "Get a specific page with its full content by ID",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pageId = new { type = "string", description = "The page GUID" }
                    },
                    required = new[] { "pageId" }
                }
            },
            new
            {
                name = "create_page",
                description = "Create a new page in the CMS",
                inputSchema = new
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
            new
            {
                name = "update_page_content",
                description = "Update the visual content/components of a page. Triggers real-time preview via SignalR.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pageId = new { type = "string", description = "The page GUID" },
                        content = new
                        {
                            type = "array",
                            description = "Array of components with type and props",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    type = new { type = "string", description = "Component name (e.g., HeroBasic, Section)" },
                                    props = new { type = "object", description = "Component properties" }
                                }
                            }
                        }
                    },
                    required = new[] { "pageId", "content" }
                }
            },
            new
            {
                name = "add_component",
                description = "Add a single component to a page. Use this for real-time building - each component appears instantly in the preview.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pageId = new { type = "string", description = "The page GUID" },
                        component = new
                        {
                            type = "object",
                            description = "Component object with type and props"
                        },
                        position = new { type = "number", description = "Insert position (default: end)" }
                    },
                    required = new[] { "pageId", "component" }
                }
            },
            new
            {
                name = "start_building",
                description = "Signal that you're starting to build a page. Shows a progress indicator to the user.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pageId = new { type = "string", description = "The page GUID" },
                        message = new { type = "string", description = "Message to show user (e.g., 'Building your landing page...')" }
                    },
                    required = new[] { "pageId" }
                }
            },
            new
            {
                name = "finish_building",
                description = "Signal that you've finished building the page. Hides the progress indicator.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pageId = new { type = "string", description = "The page GUID" }
                    },
                    required = new[] { "pageId" }
                }
            },
            new
            {
                name = "list_components",
                description = "List all 100+ available visual components you can use to build pages. Components are organized by category: Heroes, Layout, Cards, Typography, Media, Navigation, Data Display, Social, Interactive, Forms, Content, CTA, E-Commerce, Maps, Utility.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string", description = "Filter by category (heroes, layout, cards, typography, media, navigation, data, social, interactive, forms, content, cta, ecommerce, maps, utility)" }
                    }
                }
            },
            new
            {
                name = "get_component_schema",
                description = "Get the detailed schema for a specific component including all available props and their types",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        componentName = new { type = "string", description = "Component name (e.g., HeroBasic, PricingCard, Features)" }
                    },
                    required = new[] { "componentName" }
                }
            },
            new
            {
                name = "delete_page",
                description = "Delete a page from the CMS",
                inputSchema = new
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
    }
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

#endregion
