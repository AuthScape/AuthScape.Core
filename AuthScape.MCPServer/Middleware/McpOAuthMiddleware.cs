using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthScape.MCPServer.Middleware;

/// <summary>
/// Middleware to handle MCP OAuth endpoints.
/// Implements RFC 8414 (OAuth Authorization Server Metadata) and RFC 7591 (Dynamic Client Registration).
/// Claude Desktop requires these endpoints to discover OAuth configuration for remote MCP servers.
/// </summary>
public class McpOAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpOAuthMiddleware> _logger;

    private static readonly string[] AllowedRedirectSchemes = { "http", "https" };
    private static readonly string[] AllowedLocalhostHosts = { "localhost", "127.0.0.1", "[::1]" };

    public McpOAuthMiddleware(RequestDelegate next, ILogger<McpOAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Handle OAuth Authorization Server Metadata (RFC 8414)
        if (path == "/.well-known/oauth-authorization-server" && context.Request.Method == "GET")
        {
            await HandleOAuthMetadata(context);
            return;
        }

        // Handle Dynamic Client Registration (RFC 7591)
        if (path == "/register" && context.Request.Method == "POST")
        {
            await HandleClientRegistration(context);
            return;
        }

        // Pass through to next middleware
        await _next(context);
    }

    private async Task HandleOAuthMetadata(HttpContext context)
    {
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

        var metadata = new Dictionary<string, object>
        {
            ["issuer"] = baseUrl,
            ["authorization_endpoint"] = $"{baseUrl}/authorize",
            ["token_endpoint"] = $"{baseUrl}/token",
            ["registration_endpoint"] = $"{baseUrl}/register",
            ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_basic", "client_secret_post" },
            ["grant_types_supported"] = new[] { "authorization_code", "refresh_token" },
            ["response_types_supported"] = new[] { "code" },
            ["response_modes_supported"] = new[] { "query" },
            ["code_challenge_methods_supported"] = new[] { "S256" },
            ["scopes_supported"] = new[] { "openid", "profile", "email", "offline_access" },
            ["subject_types_supported"] = new[] { "public" }
        };

        _logger.LogInformation("MCP OAuth metadata requested from {RemoteIp}", context.Connection.RemoteIpAddress);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;
        await context.Response.WriteAsJsonAsync(metadata);
    }

    private async Task HandleClientRegistration(HttpContext context)
    {
        try
        {
            var applicationManager = context.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();

            ClientRegistrationRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync<ClientRegistrationRequest>(
                    context.Request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                await WriteError(context, 400, "invalid_client_metadata", "Invalid JSON in request body");
                return;
            }

            if (request == null)
            {
                await WriteError(context, 400, "invalid_client_metadata", "Request body is required");
                return;
            }

            var validatedRedirectUris = ValidateRedirectUris(request.RedirectUris);
            if (validatedRedirectUris.Count == 0)
            {
                await WriteError(context, 400, "invalid_redirect_uri",
                    "At least one valid redirect_uri is required. Only localhost or HTTPS URIs are allowed.");
                return;
            }

            var clientId = $"mcp-{GenerateSecureId()}";
            var clientSecret = GenerateSecureSecret();
            var clientName = SanitizeClientName(request.ClientName) ?? "MCP Client";

            // Ensure unique client ID
            if (await applicationManager.FindByClientIdAsync(clientId) != null)
            {
                clientId = $"mcp-{GenerateSecureId()}";
            }

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                DisplayName = clientName,
                ClientType = ClientTypes.Confidential,
                ConsentType = ConsentTypes.Explicit,
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.Revocation,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles,
                    Permissions.Prefixes.Scope + "offline_access",
                },
                Requirements =
                {
                    Requirements.Features.ProofKeyForCodeExchange
                }
            };

            foreach (var uri in validatedRedirectUris)
            {
                descriptor.RedirectUris.Add(uri);
            }

            await applicationManager.CreateAsync(descriptor);

            _logger.LogInformation(
                "MCP client registered: ClientId={ClientId}, Name={ClientName}, RedirectUris={RedirectUris}, RemoteIp={RemoteIp}",
                clientId, clientName, string.Join(", ", validatedRedirectUris), context.Connection.RemoteIpAddress);

            var response = new Dictionary<string, object>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["client_secret_expires_at"] = 0,
                ["client_name"] = clientName,
                ["redirect_uris"] = validatedRedirectUris.Select(u => u.ToString()).ToArray(),
                ["grant_types"] = new[] { "authorization_code", "refresh_token" },
                ["response_types"] = new[] { "code" },
                ["token_endpoint_auth_method"] = "client_secret_basic"
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 201;
            await context.Response.WriteAsJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering MCP client");
            await WriteError(context, 500, "server_error", "An error occurred during client registration");
        }
    }

    private static async Task WriteError(HttpContext context, int statusCode, string error, string description)
    {
        var errorResponse = new { error, error_description = description };
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(errorResponse);
    }

    private static List<Uri> ValidateRedirectUris(string[]? redirectUris)
    {
        var validUris = new List<Uri>();

        if (redirectUris == null || redirectUris.Length == 0)
            return validUris;

        foreach (var uriString in redirectUris)
        {
            if (string.IsNullOrWhiteSpace(uriString))
                continue;

            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                continue;

            if (!AllowedRedirectSchemes.Contains(uri.Scheme.ToLowerInvariant()))
                continue;

            if (AllowedLocalhostHosts.Contains(uri.Host.ToLowerInvariant()))
            {
                validUris.Add(uri);
                continue;
            }

            if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                validUris.Add(uri);
            }
        }

        return validUris;
    }

    private static string GenerateSecureId()
    {
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateSecureSecret()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string? SanitizeClientName(string? clientName)
    {
        if (string.IsNullOrWhiteSpace(clientName))
            return null;

        var sanitized = Regex.Replace(clientName, @"<[^>]*>", "");
        sanitized = sanitized.Trim();

        if (sanitized.Length > 100)
            sanitized = sanitized.Substring(0, 100);

        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    private class ClientRegistrationRequest
    {
        [JsonPropertyName("client_name")]
        public string? ClientName { get; set; }

        [JsonPropertyName("redirect_uris")]
        public string[]? RedirectUris { get; set; }

        [JsonPropertyName("grant_types")]
        public string[]? GrantTypes { get; set; }

        [JsonPropertyName("response_types")]
        public string[]? ResponseTypes { get; set; }

        [JsonPropertyName("token_endpoint_auth_method")]
        public string? TokenEndpointAuthMethod { get; set; }
    }
}

public static class McpOAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseMcpOAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<McpOAuthMiddleware>();
    }
}
