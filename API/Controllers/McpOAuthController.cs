using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace API.Controllers
{
    /// <summary>
    /// OAuth metadata and dynamic client registration endpoints for MCP (Model Context Protocol).
    /// Implements RFC 8414 (OAuth 2.0 Authorization Server Metadata) and RFC 7591 (Dynamic Client Registration).
    /// Claude Desktop requires these endpoints to discover OAuth configuration for remote MCP servers.
    /// </summary>
    [ApiController]
    public class McpOAuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IOpenIddictApplicationManager _applicationManager;
        private readonly ILogger<McpOAuthController> _logger;

        // Allowed redirect URI schemes for MCP clients (security: restrict to known safe schemes)
        private static readonly string[] AllowedRedirectSchemes = { "http", "https" };

        // Allowed redirect hosts for localhost development
        private static readonly string[] AllowedLocalhostHosts = { "localhost", "127.0.0.1", "[::1]" };

        public McpOAuthController(
            IConfiguration configuration,
            IOpenIddictApplicationManager applicationManager,
            ILogger<McpOAuthController> logger)
        {
            _configuration = configuration;
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
            var idpUrl = GetIdpUrl();
            var apiUrl = GetApiUrl();

            var metadata = new OAuthMetadata
            {
                Issuer = idpUrl,
                AuthorizationEndpoint = $"{idpUrl}/connect/authorize",
                TokenEndpoint = $"{idpUrl}/connect/token",
                UserinfoEndpoint = $"{idpUrl}/connect/userinfo",
                EndSessionEndpoint = $"{idpUrl}/connect/endsession",
                RegistrationEndpoint = $"{apiUrl}/.well-known/oauth-authorization-server/register",

                // Security: Only support secure authentication methods
                TokenEndpointAuthMethodsSupported = new[] { "client_secret_basic", "client_secret_post" },

                // Security: Only support authorization code flow (most secure for user-facing apps)
                GrantTypesSupported = new[] { "authorization_code", "refresh_token" },
                ResponseTypesSupported = new[] { "code" },

                // Security: Require PKCE with S256
                CodeChallengeMethodsSupported = new[] { "S256" },

                // Scopes available
                ScopesSupported = new[] { "openid", "profile", "email", "offline_access", "api1" },

                // Response modes
                ResponseModesSupported = new[] { "query", "fragment" },

                // Subject types
                SubjectTypesSupported = new[] { "public" }
            };

            _logger.LogInformation("OAuth metadata requested from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);

            return Ok(metadata);
        }

        /// <summary>
        /// Dynamic Client Registration endpoint (RFC 7591).
        /// Allows MCP clients like Claude Desktop to register themselves.
        /// Security: Validates redirect URIs, generates cryptographically secure secrets.
        /// </summary>
        [HttpPost("/.well-known/oauth-authorization-server/register")]
        [ProducesResponseType(typeof(ClientRegistrationResponse), 201)]
        [ProducesResponseType(typeof(ClientRegistrationError), 400)]
        public async Task<IActionResult> RegisterClient([FromBody] ClientRegistrationRequest? request)
        {
            try
            {
                // Validate request
                if (request == null)
                {
                    return BadRequest(new ClientRegistrationError
                    {
                        Error = "invalid_client_metadata",
                        ErrorDescription = "Request body is required"
                    });
                }

                // Validate redirect URIs
                var validatedRedirectUris = ValidateRedirectUris(request.RedirectUris);
                if (validatedRedirectUris.Count == 0)
                {
                    return BadRequest(new ClientRegistrationError
                    {
                        Error = "invalid_redirect_uri",
                        ErrorDescription = "At least one valid redirect_uri is required. Only localhost or HTTPS URIs are allowed."
                    });
                }

                // Generate cryptographically secure client credentials
                var clientId = $"mcp-{GenerateSecureId()}";
                var clientSecret = GenerateSecureSecret();
                var clientName = SanitizeClientName(request.ClientName) ?? "MCP Client";

                // Check if client already exists (shouldn't happen with random IDs, but be safe)
                if (await _applicationManager.FindByClientIdAsync(clientId) != null)
                {
                    clientId = $"mcp-{GenerateSecureId()}"; // Generate new ID
                }

                // Create OpenIddict application descriptor
                var descriptor = new OpenIddictApplicationDescriptor
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    DisplayName = clientName,
                    ClientType = ClientTypes.Confidential,
                    ConsentType = ConsentTypes.Explicit,
                    Permissions =
                    {
                        // Endpoints
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.Endpoints.Revocation,

                        // Grant types
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.GrantTypes.RefreshToken,

                        // Response types
                        Permissions.ResponseTypes.Code,

                        // Scopes
                        Permissions.Scopes.Email,
                        Permissions.Scopes.Profile,
                        Permissions.Scopes.Roles,
                        Permissions.Prefixes.Scope + "api1",
                    },
                    Requirements =
                    {
                        // Security: Require PKCE for all MCP clients
                        Requirements.Features.ProofKeyForCodeExchange
                    }
                };

                // Add validated redirect URIs
                foreach (var uri in validatedRedirectUris)
                {
                    descriptor.RedirectUris.Add(uri);
                }

                // Register the client in OpenIddict
                await _applicationManager.CreateAsync(descriptor);

                _logger.LogInformation(
                    "MCP client registered: ClientId={ClientId}, Name={ClientName}, RedirectUris={RedirectUris}, RemoteIp={RemoteIp}",
                    clientId, clientName, string.Join(", ", validatedRedirectUris), HttpContext.Connection.RemoteIpAddress);

                // Return registration response per RFC 7591
                var response = new ClientRegistrationResponse
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    ClientSecretExpiresAt = 0, // Never expires (0 per RFC 7591)
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

        private string GetIdpUrl()
        {
            // Try multiple config locations for IDP URL
            var idpUrl = _configuration["DnsRecords:IdentityServer"]?.TrimEnd('/')
                      ?? _configuration["AppSettings:IDPUrl"]?.TrimEnd('/');

            if (string.IsNullOrEmpty(idpUrl))
            {
                // Fallback to current host if IDP URL not configured
                idpUrl = GetApiUrl();
            }
            return idpUrl;
        }

        private string GetApiUrl()
        {
            return $"{Request.Scheme}://{Request.Host}";
        }

        /// <summary>
        /// Validates redirect URIs for security.
        /// Only allows localhost (any scheme) or HTTPS URIs to prevent token interception.
        /// </summary>
        private List<Uri> ValidateRedirectUris(string[]? redirectUris)
        {
            var validUris = new List<Uri>();

            if (redirectUris == null || redirectUris.Length == 0)
            {
                return validUris;
            }

            foreach (var uriString in redirectUris)
            {
                if (string.IsNullOrWhiteSpace(uriString))
                    continue;

                if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                    continue;

                // Security: Only allow known safe schemes
                if (!AllowedRedirectSchemes.Contains(uri.Scheme.ToLowerInvariant()))
                    continue;

                // Allow localhost with any scheme (for development)
                if (AllowedLocalhostHosts.Contains(uri.Host.ToLowerInvariant()))
                {
                    validUris.Add(uri);
                    continue;
                }

                // For non-localhost, require HTTPS
                if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    validUris.Add(uri);
                }
            }

            return validUris;
        }

        /// <summary>
        /// Generates a cryptographically secure random ID.
        /// </summary>
        private static string GenerateSecureId()
        {
            var bytes = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Generates a cryptographically secure client secret.
        /// </summary>
        private static string GenerateSecureSecret()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Sanitizes client name to prevent injection attacks.
        /// </summary>
        private static string? SanitizeClientName(string? clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                return null;

            // Remove any HTML/script tags and limit length
            var sanitized = System.Text.RegularExpressions.Regex.Replace(clientName, @"<[^>]*>", "");
            sanitized = sanitized.Trim();

            // Limit to reasonable length
            if (sanitized.Length > 100)
                sanitized = sanitized.Substring(0, 100);

            return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
        }

        #endregion
    }

    #region DTOs

    /// <summary>
    /// OAuth 2.0 Authorization Server Metadata per RFC 8414.
    /// </summary>
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

    /// <summary>
    /// Client Registration Request per RFC 7591.
    /// </summary>
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

    /// <summary>
    /// Client Registration Response per RFC 7591.
    /// </summary>
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

    /// <summary>
    /// Client Registration Error per RFC 7591.
    /// </summary>
    public class ClientRegistrationError
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    #endregion
}
