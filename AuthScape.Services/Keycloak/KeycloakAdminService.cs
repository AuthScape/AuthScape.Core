using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AuthScape.Services.Keycloak
{
    /// <summary>
    /// HttpClient-backed implementation of <see cref="IKeycloakAdminService"/>.
    /// Caches a service-account access token until expiry minus a 30-second safety window, refreshing on demand.
    /// All Keycloak failures are normalized to <see cref="KeycloakAdminException"/> with a typed
    /// <see cref="KeycloakAdminFailureKind"/> so the controller can map to the right HTTP response.
    /// </summary>
    public class KeycloakAdminService : IKeycloakAdminService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient http;
        private readonly KeycloakSettings settings;
        private readonly ILogger<KeycloakAdminService> logger;
        private readonly SemaphoreSlim tokenLock = new(1, 1);
        private string cachedToken;
        private DateTime cachedTokenExpiresUtc;

        public KeycloakAdminService(HttpClient http, IOptions<AppSettings> appSettings, ILogger<KeycloakAdminService> logger)
        {
            this.http = http ?? throw new ArgumentNullException(nameof(http));
            this.settings = appSettings?.Value?.Keycloak ?? new KeycloakSettings();
            this.logger = logger;
        }

        public async Task<KeycloakHealthDto> CheckHealthAsync()
        {
            if (!settings.Enabled)
                return new KeycloakHealthDto { Status = KeycloakHealthStatus.Disabled };

            if (!IsConfigured())
                return new KeycloakHealthDto { Status = KeycloakHealthStatus.Misconfigured, Detail = "AdminBaseUrl, Realm, AdminClientId, and AdminClientSecret are required" };

            try
            {
                using var request = await BuildAuthorizedRequestAsync(HttpMethod.Get, $"/admin/realms/{Uri.EscapeDataString(settings.Realm)}");
                using var response = await http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                    return new KeycloakHealthDto { Status = KeycloakHealthStatus.Ok };
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    return new KeycloakHealthDto { Status = KeycloakHealthStatus.Unauthorized, Detail = $"HTTP {(int)response.StatusCode}" };

                return new KeycloakHealthDto { Status = KeycloakHealthStatus.Unreachable, Detail = $"HTTP {(int)response.StatusCode}" };
            }
            catch (KeycloakAdminException ex) when (ex.Kind == KeycloakAdminFailureKind.Unauthorized)
            {
                return new KeycloakHealthDto { Status = KeycloakHealthStatus.Unauthorized, Detail = ex.Message };
            }
            catch (Exception ex)
            {
                return new KeycloakHealthDto { Status = KeycloakHealthStatus.Unreachable, Detail = ex.Message };
            }
        }

        public Task<List<KeycloakClientDto>> GetClientsAsync() =>
            SendAsync<List<KeycloakClientDto>>(HttpMethod.Get, $"/admin/realms/{Realm}/clients");

        public Task<KeycloakClientDto> GetClientAsync(string id) =>
            SendAsync<KeycloakClientDto>(HttpMethod.Get, $"/admin/realms/{Realm}/clients/{Uri.EscapeDataString(id)}");

        public async Task<string> CreateClientAsync(KeycloakClientCreateDto dto)
        {
            using var response = await SendRawAsync(HttpMethod.Post, $"/admin/realms/{Realm}/clients", dto);
            // Keycloak returns 201 with Location header containing the new resource ID.
            var location = response.Headers.Location?.ToString();
            if (!string.IsNullOrEmpty(location))
            {
                var idx = location.LastIndexOf('/');
                if (idx >= 0 && idx < location.Length - 1)
                    return location[(idx + 1)..];
            }
            return null;
        }

        public Task UpdateClientAsync(KeycloakClientUpdateDto dto) =>
            SendAsync(HttpMethod.Put, $"/admin/realms/{Realm}/clients/{Uri.EscapeDataString(dto.Id)}", dto);

        public Task DeleteClientAsync(string id) =>
            SendAsync(HttpMethod.Delete, $"/admin/realms/{Realm}/clients/{Uri.EscapeDataString(id)}");

        public Task<List<KeycloakUserDto>> GetUsersAsync(int first = 0, int max = 100, string search = null)
        {
            var query = $"first={first}&max={max}";
            if (!string.IsNullOrWhiteSpace(search))
                query += $"&search={Uri.EscapeDataString(search)}";
            return SendAsync<List<KeycloakUserDto>>(HttpMethod.Get, $"/admin/realms/{Realm}/users?{query}");
        }

        public Task<KeycloakUserDto> GetUserAsync(string id) =>
            SendAsync<KeycloakUserDto>(HttpMethod.Get, $"/admin/realms/{Realm}/users/{Uri.EscapeDataString(id)}");

        public async Task<string> CreateUserAsync(KeycloakUserCreateDto dto)
        {
            var payload = new
            {
                username = dto.Username,
                email = dto.Email,
                firstName = dto.FirstName,
                lastName = dto.LastName,
                enabled = dto.Enabled,
                emailVerified = dto.EmailVerified,
                credentials = !string.IsNullOrEmpty(dto.InitialPassword)
                    ? new[] { new { type = "password", value = dto.InitialPassword, temporary = dto.TemporaryPassword } }
                    : null
            };
            using var response = await SendRawAsync(HttpMethod.Post, $"/admin/realms/{Realm}/users", payload);
            var location = response.Headers.Location?.ToString();
            if (!string.IsNullOrEmpty(location))
            {
                var idx = location.LastIndexOf('/');
                if (idx >= 0 && idx < location.Length - 1)
                    return location[(idx + 1)..];
            }
            return null;
        }

        public Task UpdateUserAsync(KeycloakUserUpdateDto dto) =>
            SendAsync(HttpMethod.Put, $"/admin/realms/{Realm}/users/{Uri.EscapeDataString(dto.Id)}", dto);

        public Task DeleteUserAsync(string id) =>
            SendAsync(HttpMethod.Delete, $"/admin/realms/{Realm}/users/{Uri.EscapeDataString(id)}");

        public Task SendPasswordResetEmailAsync(string userId) =>
            SendAsync(HttpMethod.Put,
                $"/admin/realms/{Realm}/users/{Uri.EscapeDataString(userId)}/execute-actions-email",
                new[] { "UPDATE_PASSWORD" });

        public Task<List<KeycloakClientScopeDto>> GetClientScopesAsync() =>
            SendAsync<List<KeycloakClientScopeDto>>(HttpMethod.Get, $"/admin/realms/{Realm}/client-scopes");

        public Task CreateClientScopeAsync(KeycloakClientScopeCreateDto dto) =>
            SendAsync(HttpMethod.Post, $"/admin/realms/{Realm}/client-scopes", dto);

        public Task UpdateClientScopeAsync(KeycloakClientScopeUpdateDto dto) =>
            SendAsync(HttpMethod.Put, $"/admin/realms/{Realm}/client-scopes/{Uri.EscapeDataString(dto.Id)}", dto);

        public Task DeleteClientScopeAsync(string id) =>
            SendAsync(HttpMethod.Delete, $"/admin/realms/{Realm}/client-scopes/{Uri.EscapeDataString(id)}");

        // ---- internals ----

        private string Realm => Uri.EscapeDataString(settings.Realm ?? "");

        private bool IsConfigured() =>
            !string.IsNullOrWhiteSpace(settings.AdminBaseUrl)
            && !string.IsNullOrWhiteSpace(settings.Realm)
            && !string.IsNullOrWhiteSpace(settings.AdminClientId)
            && !string.IsNullOrWhiteSpace(settings.AdminClientSecret);

        private void EnsureEnabledAndConfigured()
        {
            if (!settings.Enabled)
                throw new KeycloakAdminException(KeycloakAdminFailureKind.Disabled, "Keycloak admin integration is disabled (set Keycloak.Enabled = true in appsettings).");
            if (!IsConfigured())
                throw new KeycloakAdminException(KeycloakAdminFailureKind.Misconfigured, "Keycloak admin integration is enabled but missing required settings (AdminBaseUrl, Realm, AdminClientId, AdminClientSecret).");
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (cachedToken != null && DateTime.UtcNow < cachedTokenExpiresUtc.AddSeconds(-30))
                return cachedToken;

            await tokenLock.WaitAsync();
            try
            {
                if (cachedToken != null && DateTime.UtcNow < cachedTokenExpiresUtc.AddSeconds(-30))
                    return cachedToken;

                var tokenUrl = $"{settings.AdminBaseUrl.TrimEnd('/')}/realms/{Uri.EscapeDataString(settings.Realm)}/protocol/openid-connect/token";
                using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
                request.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("client_id", settings.AdminClientId),
                    new KeyValuePair<string, string>("client_secret", settings.AdminClientSecret)
                });

                HttpResponseMessage response;
                try
                {
                    response = await http.SendAsync(request);
                }
                catch (Exception ex)
                {
                    throw new KeycloakAdminException(KeycloakAdminFailureKind.Unreachable, $"Could not reach Keycloak token endpoint: {ex.Message}", ex);
                }

                using (response)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                        throw new KeycloakAdminException(KeycloakAdminFailureKind.Unauthorized, $"Keycloak rejected admin client credentials (HTTP {(int)response.StatusCode}).");

                    if (!response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        throw new KeycloakAdminException(KeycloakAdminFailureKind.Unexpected, $"Keycloak token endpoint returned HTTP {(int)response.StatusCode}: {Truncate(body, 500)}");
                    }

                    var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                    if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                        throw new KeycloakAdminException(KeycloakAdminFailureKind.Unexpected, "Keycloak token endpoint returned an empty access_token.");

                    cachedToken = tokenResponse.AccessToken;
                    cachedTokenExpiresUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 60);
                    return cachedToken;
                }
            }
            finally
            {
                tokenLock.Release();
            }
        }

        private async Task<HttpRequestMessage> BuildAuthorizedRequestAsync(HttpMethod method, string path, object body = null)
        {
            EnsureEnabledAndConfigured();
            var url = $"{settings.AdminBaseUrl.TrimEnd('/')}{path}";
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync());
            if (body != null)
                request.Content = JsonContent.Create(body, options: JsonOptions);
            return request;
        }

        private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object body = null, bool retriedOnUnauthorized = false)
        {
            HttpRequestMessage request = null;
            HttpResponseMessage response;
            try
            {
                request = await BuildAuthorizedRequestAsync(method, path, body);
                response = await http.SendAsync(request);
            }
            catch (KeycloakAdminException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Keycloak admin request {Method} {Path} failed at the transport layer", method, path);
                throw new KeycloakAdminException(KeycloakAdminFailureKind.Unreachable, $"Could not reach Keycloak admin API: {ex.Message}", ex);
            }
            finally
            {
                request?.Dispose();
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && !retriedOnUnauthorized)
            {
                response.Dispose();
                cachedToken = null; // force refresh
                return await SendRawAsync(method, path, body, retriedOnUnauthorized: true);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                var body403 = await response.Content.ReadAsStringAsync();
                response.Dispose();
                throw new KeycloakAdminException(KeycloakAdminFailureKind.Unauthorized, $"Keycloak refused the admin request (HTTP {(int)response.StatusCode}): {Truncate(body403, 500)}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var bodyText = await response.Content.ReadAsStringAsync();
                response.Dispose();
                throw new KeycloakAdminException(KeycloakAdminFailureKind.Unexpected, $"Keycloak admin API returned HTTP {(int)response.StatusCode}: {Truncate(bodyText, 500)}");
            }

            return response;
        }

        private async Task SendAsync(HttpMethod method, string path, object body = null)
        {
            using var _ = await SendRawAsync(method, path, body);
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object body = null)
        {
            using var response = await SendRawAsync(method, path, body);
            if (response.Content == null || response.Content.Headers.ContentLength == 0)
                return default;
            try
            {
                return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new KeycloakAdminException(KeycloakAdminFailureKind.Unexpected, $"Could not parse Keycloak response as {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "...";

        private class TokenResponse
        {
            [JsonPropertyName("access_token")] public string AccessToken { get; set; }
            [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
            [JsonPropertyName("token_type")] public string TokenType { get; set; }
        }
    }
}
