using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IDP.Controllers
{
    /// <summary>
    /// OAuth metadata and dynamic client registration endpoints for MCP (Model Context Protocol).
    /// Implements RFC 8414 (OAuth 2.0 Authorization Server Metadata) and RFC 7591 (Dynamic Client Registration).
    /// Claude Desktop requires these endpoints to discover OAuth configuration for remote MCP servers.
    /// </summary>
    [ApiController]
    public class McpOAuthController : ControllerBase
    {
        private readonly IOpenIddictApplicationManager _applicationManager;
        private readonly ILogger<McpOAuthController> _logger;

        // Allowed redirect URI schemes for MCP clients
        private static readonly string[] AllowedRedirectSchemes = { "http", "https" };
        private static readonly string[] AllowedLocalhostHosts = { "localhost", "127.0.0.1", "[::1]" };

        public McpOAuthController(
            IOpenIddictApplicationManager applicationManager,
            ILogger<McpOAuthController> logger)
        {
            _applicationManager = applicationManager;
            _logger = logger;
        }

        /// <summary>
        /// OAuth 2.0 Authorization Server Metadata (RFC 8414).
        /// Claude Desktop calls this to discover OAuth endpoints.
        /// </summary>
        [HttpGet("/.well-known/oauth-authorization-server")]
        [ProducesResponseType(typeof(OAuthMetadata), 200)]
        public IActionResult GetOAuthMetadata()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var metadata = new OAuthMetadata
            {
                Issuer = baseUrl,
                AuthorizationEndpoint = $"{baseUrl}/connect/authorize",
                TokenEndpoint = $"{baseUrl}/connect/token",
                UserinfoEndpoint = $"{baseUrl}/connect/userinfo",
                EndSessionEndpoint = $"{baseUrl}/connect/endsession",
                RegistrationEndpoint = $"{baseUrl}/.well-known/oauth-authorization-server/register",

                TokenEndpointAuthMethodsSupported = new[] { "client_secret_basic", "client_secret_post" },
                GrantTypesSupported = new[] { "authorization_code", "refresh_token" },
                ResponseTypesSupported = new[] { "code" },
                CodeChallengeMethodsSupported = new[] { "S256" },
                ScopesSupported = new[] { "openid", "profile", "email", "offline_access", "api1" },
                ResponseModesSupported = new[] { "query", "fragment" },
                SubjectTypesSupported = new[] { "public" }
            };

            _logger.LogInformation("OAuth metadata requested from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);

            return Ok(metadata);
        }

        /// <summary>
        /// Dynamic Client Registration endpoint (RFC 7591).
        /// Allows MCP clients like Claude Desktop to register themselves.
        /// </summary>
        [HttpPost("/.well-known/oauth-authorization-server/register")]
        [ProducesResponseType(typeof(ClientRegistrationResponse), 201)]
        [ProducesResponseType(typeof(ClientRegistrationError), 400)]
        public async Task<IActionResult> RegisterClient([FromBody] ClientRegistrationRequest? request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new ClientRegistrationError
                    {
                        Error = "invalid_client_metadata",
                        ErrorDescription = "Request body is required"
                    });
                }

                var validatedRedirectUris = ValidateRedirectUris(request.RedirectUris);
                if (validatedRedirectUris.Count == 0)
                {
                    return BadRequest(new ClientRegistrationError
                    {
                        Error = "invalid_redirect_uri",
                        ErrorDescription = "At least one valid redirect_uri is required. Only localhost or HTTPS URIs are allowed."
                    });
                }

                var clientId = $"mcp-{GenerateSecureId()}";
                var clientSecret = GenerateSecureSecret();
                var clientName = SanitizeClientName(request.ClientName) ?? "MCP Client";

                if (await _applicationManager.FindByClientIdAsync(clientId) != null)
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
                        Permissions.Prefixes.Scope + "api1",
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

                await _applicationManager.CreateAsync(descriptor);

                _logger.LogInformation(
                    "MCP client registered: ClientId={ClientId}, Name={ClientName}, RedirectUris={RedirectUris}, RemoteIp={RemoteIp}",
                    clientId, clientName, string.Join(", ", validatedRedirectUris), HttpContext.Connection.RemoteIpAddress);

                var response = new ClientRegistrationResponse
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    ClientSecretExpiresAt = 0,
                    ClientName = clientName,
                    RedirectUris = validatedRedirectUris.Select(u => u.ToString()).ToArray(),
                    GrantTypes = new[] { "authorization_code", "refresh_token" },
                    ResponseTypes = new[] { "code" },
                    TokenEndpointAuthMethod = "client_secret_basic"
                };

                return StatusCode(201, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering MCP client");
                return BadRequest(new ClientRegistrationError
                {
                    Error = "server_error",
                    ErrorDescription = "An error occurred during client registration"
                });
            }
        }

        #region Helper Methods

        private List<Uri> ValidateRedirectUris(string[]? redirectUris)
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

            var sanitized = System.Text.RegularExpressions.Regex.Replace(clientName, @"<[^>]*>", "");
            sanitized = sanitized.Trim();

            if (sanitized.Length > 100)
                sanitized = sanitized.Substring(0, 100);

            return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
        }

        #endregion
    }

    #region OAuth DTOs

    public class OAuthMetadata
    {
        [JsonPropertyName("issuer")]
        public string Issuer { get; set; } = "";

        [JsonPropertyName("authorization_endpoint")]
        public string AuthorizationEndpoint { get; set; } = "";

        [JsonPropertyName("token_endpoint")]
        public string TokenEndpoint { get; set; } = "";

        [JsonPropertyName("userinfo_endpoint")]
        public string? UserinfoEndpoint { get; set; }

        [JsonPropertyName("end_session_endpoint")]
        public string? EndSessionEndpoint { get; set; }

        [JsonPropertyName("registration_endpoint")]
        public string? RegistrationEndpoint { get; set; }

        [JsonPropertyName("token_endpoint_auth_methods_supported")]
        public string[] TokenEndpointAuthMethodsSupported { get; set; } = Array.Empty<string>();

        [JsonPropertyName("grant_types_supported")]
        public string[] GrantTypesSupported { get; set; } = Array.Empty<string>();

        [JsonPropertyName("response_types_supported")]
        public string[] ResponseTypesSupported { get; set; } = Array.Empty<string>();

        [JsonPropertyName("response_modes_supported")]
        public string[] ResponseModesSupported { get; set; } = Array.Empty<string>();

        [JsonPropertyName("code_challenge_methods_supported")]
        public string[] CodeChallengeMethodsSupported { get; set; } = Array.Empty<string>();

        [JsonPropertyName("scopes_supported")]
        public string[] ScopesSupported { get; set; } = Array.Empty<string>();

        [JsonPropertyName("subject_types_supported")]
        public string[] SubjectTypesSupported { get; set; } = Array.Empty<string>();
    }

    public class ClientRegistrationRequest
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

    public class ClientRegistrationResponse
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = "";

        [JsonPropertyName("client_secret")]
        public string? ClientSecret { get; set; }

        [JsonPropertyName("client_secret_expires_at")]
        public long ClientSecretExpiresAt { get; set; }

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

    public class ClientRegistrationError
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    #endregion
}
