using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Services.Context;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthScape.MCPServer.Controllers;

/// <summary>
/// OAuth 2.0 Authorization and Token endpoints for MCP.
/// Implements Authorization Code flow with PKCE as required by Claude Desktop.
/// </summary>
[ApiController]
public class OAuthController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly DatabaseContext _dbContext;
    private readonly ILogger<OAuthController> _logger;
    private readonly IConfiguration _configuration;

    // In-memory store for authorization codes (in production, use distributed cache)
    private static readonly ConcurrentDictionary<string, AuthorizationCodeData> _authCodes = new();

    public OAuthController(
        IOpenIddictApplicationManager applicationManager,
        DatabaseContext dbContext,
        ILogger<OAuthController> logger,
        IConfiguration configuration)
    {
        _applicationManager = applicationManager;
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Authorization endpoint - initiates the OAuth flow.
    /// Claude Desktop redirects here with client_id, redirect_uri, code_challenge, etc.
    /// </summary>
    [HttpGet("/authorize")]
    public async Task<IActionResult> Authorize(
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string response_type,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        [FromQuery] string? code_challenge,
        [FromQuery] string? code_challenge_method)
    {
        _logger.LogInformation("Authorization request: client_id={ClientId}, redirect_uri={RedirectUri}",
            client_id, redirect_uri);

        // Validate client
        var application = await _applicationManager.FindByClientIdAsync(client_id);
        if (application == null)
        {
            return BadRequest(new { error = "invalid_client", error_description = "Unknown client_id" });
        }

        // Validate redirect_uri
        var redirectUris = await _applicationManager.GetRedirectUrisAsync(application);
        if (!redirectUris.Contains(redirect_uri))
        {
            return BadRequest(new { error = "invalid_request", error_description = "Invalid redirect_uri" });
        }

        // Validate response_type
        if (response_type != "code")
        {
            return BadRequest(new { error = "unsupported_response_type", error_description = "Only 'code' response_type is supported" });
        }

        // For MCP, we auto-approve and generate code immediately (no user login required for testing)
        // In production, you would redirect to a login page here
        var code = GenerateAuthorizationCode();

        _authCodes[code] = new AuthorizationCodeData
        {
            ClientId = client_id,
            RedirectUri = redirect_uri,
            Scope = scope ?? "openid",
            CodeChallenge = code_challenge,
            CodeChallengeMethod = code_challenge_method,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        // Build redirect URL with code
        var redirectBuilder = new UriBuilder(redirect_uri);
        var query = System.Web.HttpUtility.ParseQueryString(redirectBuilder.Query);
        query["code"] = code;
        if (!string.IsNullOrEmpty(state))
        {
            query["state"] = state;
        }
        redirectBuilder.Query = query.ToString();

        _logger.LogInformation("Issuing authorization code for client {ClientId}", client_id);

        return Redirect(redirectBuilder.ToString());
    }

    /// <summary>
    /// Token endpoint - exchanges authorization code for access token.
    /// </summary>
    [HttpPost("/token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token([FromForm] TokenRequest request)
    {
        _logger.LogInformation("Token request: grant_type={GrantType}, client_id={ClientId}",
            request.grant_type, request.client_id);

        // Handle authorization_code grant
        if (request.grant_type == "authorization_code")
        {
            return await HandleAuthorizationCodeGrant(request);
        }

        // Handle refresh_token grant
        if (request.grant_type == "refresh_token")
        {
            return await HandleRefreshTokenGrant(request);
        }

        return BadRequest(new { error = "unsupported_grant_type" });
    }

    private async Task<IActionResult> HandleAuthorizationCodeGrant(TokenRequest request)
    {
        if (string.IsNullOrEmpty(request.code))
        {
            return BadRequest(new { error = "invalid_request", error_description = "code is required" });
        }

        // Retrieve and validate authorization code
        if (!_authCodes.TryRemove(request.code, out var codeData))
        {
            return BadRequest(new { error = "invalid_grant", error_description = "Invalid or expired authorization code" });
        }

        if (codeData.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new { error = "invalid_grant", error_description = "Authorization code has expired" });
        }

        if (codeData.ClientId != request.client_id)
        {
            return BadRequest(new { error = "invalid_grant", error_description = "Client ID mismatch" });
        }

        if (codeData.RedirectUri != request.redirect_uri)
        {
            return BadRequest(new { error = "invalid_grant", error_description = "Redirect URI mismatch" });
        }

        // Validate PKCE code_verifier
        if (!string.IsNullOrEmpty(codeData.CodeChallenge))
        {
            if (string.IsNullOrEmpty(request.code_verifier))
            {
                return BadRequest(new { error = "invalid_grant", error_description = "code_verifier is required" });
            }

            var expectedChallenge = ComputeCodeChallenge(request.code_verifier, codeData.CodeChallengeMethod);
            if (expectedChallenge != codeData.CodeChallenge)
            {
                return BadRequest(new { error = "invalid_grant", error_description = "Invalid code_verifier" });
            }
        }

        // Validate client credentials
        var application = await _applicationManager.FindByClientIdAsync(request.client_id);
        if (application == null)
        {
            return BadRequest(new { error = "invalid_client" });
        }

        // Check client_secret if provided (from Basic auth or body)
        var clientSecret = request.client_secret ?? ExtractClientSecretFromBasicAuth();
        if (!string.IsNullOrEmpty(clientSecret))
        {
            if (!await _applicationManager.ValidateClientSecretAsync(application, clientSecret))
            {
                return BadRequest(new { error = "invalid_client", error_description = "Invalid client credentials" });
            }
        }

        // Generate tokens
        var accessToken = GenerateAccessToken();
        var refreshToken = GenerateRefreshToken();

        _logger.LogInformation("Issuing tokens for client {ClientId}", request.client_id);

        return Ok(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = 3600,
            refresh_token = refreshToken,
            scope = codeData.Scope
        });
    }

    private async Task<IActionResult> HandleRefreshTokenGrant(TokenRequest request)
    {
        if (string.IsNullOrEmpty(request.refresh_token))
        {
            return BadRequest(new { error = "invalid_request", error_description = "refresh_token is required" });
        }

        // In a real implementation, validate the refresh token against stored tokens
        // For now, just issue new tokens
        var accessToken = GenerateAccessToken();
        var refreshToken = GenerateRefreshToken();

        return Ok(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = 3600,
            refresh_token = refreshToken
        });
    }

    private string? ExtractClientSecretFromBasicAuth()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var base64 = authHeader.Substring(6);
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var parts = decoded.Split(':', 2);
            return parts.Length == 2 ? parts[1] : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeCodeChallenge(string codeVerifier, string? method)
    {
        if (method == "S256")
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(hash);
        }
        // Plain method
        return codeVerifier;
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateAuthorizationCode()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateAccessToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return $"mcp_{Base64UrlEncode(bytes)}";
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return $"mcp_rt_{Base64UrlEncode(bytes)}";
    }

    private class AuthorizationCodeData
    {
        public string ClientId { get; set; } = "";
        public string RedirectUri { get; set; } = "";
        public string Scope { get; set; } = "";
        public string? CodeChallenge { get; set; }
        public string? CodeChallengeMethod { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}

public class TokenRequest
{
    public string grant_type { get; set; } = "";
    public string? client_id { get; set; }
    public string? client_secret { get; set; }
    public string? code { get; set; }
    public string? redirect_uri { get; set; }
    public string? code_verifier { get; set; }
    public string? refresh_token { get; set; }
}
