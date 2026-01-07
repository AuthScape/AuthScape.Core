using System.Net.Http.Json;
using System.Text.Json;

namespace AuthScape.MCP;

/// <summary>
/// HTTP client for communicating with the AuthScape CMS API.
/// </summary>
public class AuthScapeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuthScapeApiClient(string apiUrl, string accessToken)
    {
        _baseUrl = apiUrl.TrimEnd('/');

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task<string> ListPagesAsync(JsonElement? arguments)
    {
        var search = arguments?.TryGetProperty("search", out var s) == true ? s.GetString() : "";
        var limit = arguments?.TryGetProperty("limit", out var l) == true ? l.GetInt32() : 20;

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/api/AuthscapeMCP/ListPages",
            new { search, limit });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        return result;
    }

    public async Task<string> GetPageAsync(JsonElement? arguments)
    {
        var pageId = arguments?.GetProperty("pageId").GetString()
            ?? throw new ArgumentException("pageId is required");

        var response = await _httpClient.GetAsync(
            $"{_baseUrl}/api/AuthscapeMCP/GetPage?pageId={pageId}");

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> CreatePageAsync(JsonElement? arguments)
    {
        var title = arguments?.GetProperty("title").GetString() ?? "";
        var slug = arguments?.GetProperty("slug").GetString() ?? "";
        var description = arguments?.TryGetProperty("description", out var d) == true ? d.GetString() : "";

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/api/AuthscapeMCP/CreatePage",
            new { title, slug, description });

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> UpdatePageContentAsync(JsonElement? arguments)
    {
        var pageId = arguments?.GetProperty("pageId").GetString()
            ?? throw new ArgumentException("pageId is required");
        var content = arguments?.GetProperty("content");

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/api/AuthscapeMCP/UpdatePageContent",
            new { pageId, content });

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> AddComponentAsync(JsonElement? arguments)
    {
        var pageId = arguments?.GetProperty("pageId").GetString()
            ?? throw new ArgumentException("pageId is required");
        var component = arguments?.GetProperty("component");
        var position = arguments?.TryGetProperty("position", out var p) == true ? p.GetInt32() : (int?)null;

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/api/AuthscapeMCP/AddComponent",
            new { pageId, component, position });

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> StartBuildingAsync(JsonElement? arguments)
    {
        var pageId = arguments?.GetProperty("pageId").GetString()
            ?? throw new ArgumentException("pageId is required");
        var message = arguments?.TryGetProperty("message", out var m) == true ? m.GetString() : "Building page...";

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/api/AuthscapeMCP/StartBuilding",
            new { pageId, message });

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> FinishBuildingAsync(JsonElement? arguments)
    {
        var pageId = arguments?.GetProperty("pageId").GetString()
            ?? throw new ArgumentException("pageId is required");

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/api/AuthscapeMCP/FinishBuilding",
            new { pageId });

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public string ListComponents(JsonElement? arguments)
    {
        var category = arguments?.TryGetProperty("category", out var c) == true ? c.GetString() : null;
        var result = ComponentSchemas.ListComponents(category);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    public string GetComponentSchema(JsonElement? arguments)
    {
        var componentName = arguments?.GetProperty("componentName").GetString()
            ?? throw new ArgumentException("componentName is required");
        var schema = ComponentSchemas.GetSchema(componentName);
        return JsonSerializer.Serialize(schema, _jsonOptions);
    }

    public async Task<string> DeletePageAsync(JsonElement? arguments)
    {
        var pageId = arguments?.GetProperty("pageId").GetString()
            ?? throw new ArgumentException("pageId is required");

        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/api/AuthscapeMCP/DeletePage?pageId={pageId}",
            null);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
