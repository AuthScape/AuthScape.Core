using System.Net.Http.Headers;
using System.Text;
using AuthScape.UserManageSystem.CRM.Interfaces;
using AuthScape.UserManageSystem.Models.CRM;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AuthScape.UserManageSystem.CRM.Providers;

/// <summary>
/// Base class for CRM providers with common HTTP and authentication functionality
/// </summary>
public abstract class BaseCrmProvider : ICrmProvider
{
    protected readonly HttpClient _httpClient;
    protected readonly IHttpClientFactory? _httpClientFactory;
    protected readonly ILogger _logger;

    public abstract string ProviderName { get; }

    protected BaseCrmProvider(IHttpClientFactory? httpClientFactory = null, ILogger? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _httpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    #region Abstract Methods (must be implemented by each provider)

    public abstract Task<bool> ValidateConnectionAsync(CrmConnection connection);
    public abstract Task<CrmAuthResult> RefreshTokenAsync(CrmConnection connection);
    public abstract string GetAuthorizationUrl(string redirectUri, string state);
    public abstract Task<CrmAuthResult> ExchangeCodeForTokenAsync(string code, string redirectUri);
    public abstract Task<IEnumerable<CrmEntitySchema>> GetAvailableEntitiesAsync(CrmConnection connection);
    public abstract Task<IEnumerable<CrmFieldSchema>> GetEntityFieldsAsync(CrmConnection connection, string entityName);
    public abstract Task<CrmRecord?> GetRecordAsync(CrmConnection connection, string entityName, string recordId);
    public abstract Task<IEnumerable<CrmRecord>> GetRecordsAsync(CrmConnection connection, string entityName, DateTimeOffset? modifiedSince = null, string? filter = null, int? top = null);
    public abstract Task<string> CreateRecordAsync(CrmConnection connection, string entityName, Dictionary<string, object?> fields);
    public abstract Task UpdateRecordAsync(CrmConnection connection, string entityName, string recordId, Dictionary<string, object?> fields);
    public abstract Task DeleteRecordAsync(CrmConnection connection, string entityName, string recordId);
    public abstract Task<bool> RegisterWebhookAsync(CrmConnection connection, string webhookUrl, IEnumerable<string> entityNames);
    public abstract Task<CrmWebhookEvent?> ParseWebhookPayloadAsync(string payload, IDictionary<string, string> headers);
    public abstract bool ValidateWebhookSignature(string payload, IDictionary<string, string> headers, string? secret);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Makes an authenticated GET request
    /// </summary>
    protected async Task<T?> GetAsync<T>(CrmConnection connection, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthorizationHeader(request, connection);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({response.StatusCode}): {content}");
        }

        return JsonConvert.DeserializeObject<T>(content);
    }

    /// <summary>
    /// Makes an authenticated POST request
    /// </summary>
    protected async Task<T?> PostAsync<T>(CrmConnection connection, string url, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        AddAuthorizationHeader(request, connection);

        if (body != null)
        {
            var json = JsonConvert.SerializeObject(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return string.IsNullOrEmpty(content) ? default : JsonConvert.DeserializeObject<T>(content);
    }

    /// <summary>
    /// Makes an authenticated POST request and returns the Location header (for creates)
    /// </summary>
    protected async Task<string?> PostAndGetLocationAsync(CrmConnection connection, string url, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        AddAuthorizationHeader(request, connection);

        if (body != null)
        {
            var json = JsonConvert.SerializeObject(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return response.Headers.Location?.ToString();
    }

    /// <summary>
    /// Makes an authenticated PATCH request
    /// </summary>
    protected async Task PatchAsync(CrmConnection connection, string url, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, url);
        AddAuthorizationHeader(request, connection);

        var json = JsonConvert.SerializeObject(body);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("PATCH request to {Url} with body: {Body}", url, json);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("PATCH request failed: {StatusCode} - {ReasonPhrase}. URL: {Url}. Body: {Body}. Response: {ErrorContent}",
                response.StatusCode, response.ReasonPhrase, url, json, errorContent);
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({response.StatusCode}): {errorContent}");
        }
    }

    /// <summary>
    /// Makes an authenticated DELETE request
    /// </summary>
    protected async Task DeleteAsync(CrmConnection connection, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        AddAuthorizationHeader(request, connection);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Adds the appropriate authorization header based on connection type
    /// </summary>
    protected virtual void AddAuthorizationHeader(HttpRequestMessage request, CrmConnection connection)
    {
        if (!string.IsNullOrEmpty(connection.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
        }
        else if (!string.IsNullOrEmpty(connection.ApiKey))
        {
            // Default API key header - providers can override
            request.Headers.Add("Authorization", $"Bearer {connection.ApiKey}");
        }
    }

    /// <summary>
    /// Checks if the access token is expired and needs refresh
    /// </summary>
    protected bool IsTokenExpired(CrmConnection connection)
    {
        if (connection.TokenExpiry == null) return false;
        return connection.TokenExpiry <= DateTimeOffset.UtcNow.AddMinutes(5); // 5 minute buffer
    }

    /// <summary>
    /// Gets a connection with a valid token (refreshes if needed)
    /// </summary>
    protected async Task<CrmConnection> EnsureValidTokenAsync(CrmConnection connection)
    {
        if (IsTokenExpired(connection) && !string.IsNullOrEmpty(connection.RefreshToken))
        {
            var result = await RefreshTokenAsync(connection);
            if (result.Success)
            {
                connection.AccessToken = result.AccessToken;
                connection.RefreshToken = result.RefreshToken;
                connection.TokenExpiry = result.ExpiresAt;
            }
        }
        return connection;
    }

    #endregion
}
