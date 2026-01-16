using AuthScape.CRM.Interfaces;
using AuthScape.CRM.Models;
using AuthScape.CRM.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenIddict.Validation.AspNetCore;

namespace AuthScape.CRM.Controllers;

/// <summary>
/// API controller for managing CRM connections
/// </summary>
[ApiController]
[Route("api/UserManagement/[action]")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class CrmConnectionController : ControllerBase
{
    private readonly ICrmSyncService _syncService;
    private readonly ICrmProviderFactory _providerFactory;
    private readonly ILogger<CrmConnectionController> _logger;
    private readonly IConfiguration _configuration;

    // Static dictionary to track sync progress across requests
    private static readonly Dictionary<long, SyncProgressInfo> _syncProgress = new();

    public CrmConnectionController(
        ICrmSyncService syncService,
        ICrmProviderFactory providerFactory,
        ILogger<CrmConnectionController> logger,
        IConfiguration configuration)
    {
        _syncService = syncService;
        _providerFactory = providerFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets all CRM connections for the current company
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CrmConnectionDto>>> GetCrmConnections([FromQuery] long? companyId = null)
    {
        var connections = await _syncService.GetConnectionsAsync(companyId);
        return Ok(connections.Select(MapToDto));
    }

    /// <summary>
    /// Gets a specific CRM connection by ID
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CrmConnectionDto>> GetCrmConnection(long id)
    {
        var connection = await _syncService.GetConnectionAsync(id);
        if (connection == null)
            return NotFound();

        return Ok(MapToDto(connection));
    }

    /// <summary>
    /// Gets available CRM provider types
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<CrmProviderInfo>> GetCrmProviders()
    {
        var providers = _providerFactory.GetAvailableProviders()
            .Select(p => new CrmProviderInfo
            {
                Type = p,
                Name = p.ToString(),
                SupportsOAuth = p == CrmProviderType.Dynamics365 || p == CrmProviderType.HubSpot || p == CrmProviderType.GoogleContacts,
                SupportsApiKey = p == CrmProviderType.HubSpot || p == CrmProviderType.SendGridContacts
            });

        return Ok(providers);
    }

    /// <summary>
    /// Creates a new CRM connection
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CrmConnectionDto>> CreateCrmConnection([FromBody] CreateCrmConnectionRequest request)
    {
        var connection = new CrmConnection
        {
            CompanyId = request.CompanyId,
            Provider = request.Provider,
            DisplayName = request.DisplayName,
            EnvironmentUrl = request.EnvironmentUrl,
            ApiKey = request.ApiKey,
            ClientId = request.ClientId,
            ClientSecret = request.ClientSecret,
            TenantId = request.TenantId,
            SyncDirection = request.SyncDirection,
            SyncIntervalMinutes = request.SyncIntervalMinutes ?? 15,
            IsEnabled = request.IsEnabled ?? true
        };

        var created = await _syncService.CreateConnectionAsync(connection);
        _logger.LogInformation("Created CRM connection {Id} for provider {Provider}", created.Id, created.Provider);

        return Ok(MapToDto(created));
    }

    /// <summary>
    /// Updates an existing CRM connection
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<CrmConnectionDto>> UpdateCrmConnection(long id, [FromBody] UpdateCrmConnectionRequest request)
    {
        var connection = await _syncService.GetConnectionAsync(id);
        if (connection == null)
            return NotFound();

        if (request.DisplayName != null)
            connection.DisplayName = request.DisplayName;
        if (request.EnvironmentUrl != null)
            connection.EnvironmentUrl = request.EnvironmentUrl;
        if (request.SyncDirection.HasValue)
            connection.SyncDirection = request.SyncDirection.Value;
        if (request.SyncIntervalMinutes.HasValue)
            connection.SyncIntervalMinutes = request.SyncIntervalMinutes.Value;
        if (request.IsEnabled.HasValue)
            connection.IsEnabled = request.IsEnabled.Value;

        // Update OAuth credentials if provided
        if (!string.IsNullOrEmpty(request.ClientId))
            connection.ClientId = request.ClientId;
        if (!string.IsNullOrEmpty(request.ClientSecret))
            connection.ClientSecret = request.ClientSecret;
        if (!string.IsNullOrEmpty(request.TenantId))
            connection.TenantId = request.TenantId;
        if (!string.IsNullOrEmpty(request.ApiKey))
            connection.ApiKey = request.ApiKey;

        // Clear existing token when credentials change so a new one is acquired
        if (!string.IsNullOrEmpty(request.ClientId) || !string.IsNullOrEmpty(request.ClientSecret) || !string.IsNullOrEmpty(request.TenantId))
        {
            connection.AccessToken = null;
            connection.RefreshToken = null;
            connection.TokenExpiry = null;
        }

        var updated = await _syncService.UpdateConnectionAsync(connection);
        _logger.LogInformation("Updated CRM connection {Id}", id);

        return Ok(MapToDto(updated));
    }

    /// <summary>
    /// Deletes a CRM connection
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteCrmConnection(long id)
    {
        var success = await _syncService.DeleteConnectionAsync(id);
        if (!success)
            return NotFound();

        _logger.LogInformation("Deleted CRM connection {Id}", id);
        return Ok();
    }

    /// <summary>
    /// Tests a CRM connection
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ConnectionTestResult>> TestCrmConnection(long id)
    {
        var connection = await _syncService.GetConnectionAsync(id);
        if (connection == null)
            return NotFound();

        // Check what credentials are available
        var hasConnectionCredentials = !string.IsNullOrEmpty(connection.ClientId) &&
                                       !string.IsNullOrEmpty(connection.ClientSecret) &&
                                       !string.IsNullOrEmpty(connection.TenantId);
        // Check both paths for appsettings credentials (AppSettings:CRM:... and CRM:...)
        var hasAppSettingsCredentials = !string.IsNullOrEmpty(_configuration["AppSettings:CRM:Dynamics:ClientId"] ?? _configuration["CRM:Dynamics:ClientId"]) &&
                                        !string.IsNullOrEmpty(_configuration["AppSettings:CRM:Dynamics:ClientSecret"] ?? _configuration["CRM:Dynamics:ClientSecret"]) &&
                                        !string.IsNullOrEmpty(_configuration["AppSettings:CRM:Dynamics:TenantId"] ?? _configuration["CRM:Dynamics:TenantId"]);

        if (!hasConnectionCredentials && !hasAppSettingsCredentials && string.IsNullOrEmpty(connection.AccessToken))
        {
            return Ok(new ConnectionTestResult
            {
                Success = false,
                Message = "No credentials configured. Please set ClientId, ClientSecret, and TenantId on the connection or in appsettings.json under AppSettings:CRM:Dynamics:..."
            });
        }

        var provider = _providerFactory.GetProvider(connection);

        // If it's a Dynamics provider, try to get a more detailed error
        if (provider is Providers.DynamicsProvider dynamicsProvider)
        {
            // Try to acquire a token first to get any errors
            var tokenResult = await dynamicsProvider.AcquireTokenWithClientCredentialsAsync(connection);
            if (!tokenResult.Success)
            {
                var credSource = hasConnectionCredentials ? "connection" : "appsettings";
                return Ok(new ConnectionTestResult
                {
                    Success = false,
                    Message = $"OAuth token acquisition failed (using {credSource} credentials). Error: {tokenResult.Error} - {tokenResult.ErrorDescription}"
                });
            }

            // Token acquired, now validate the connection
            connection.AccessToken = tokenResult.AccessToken;
            connection.TokenExpiry = tokenResult.ExpiresAt;
        }

        var isValid = await provider.ValidateConnectionAsync(connection);

        return Ok(new ConnectionTestResult
        {
            Success = isValid,
            Message = isValid ? "Connection successful!" : "Connection validation failed. Could not connect to Dynamics API."
        });
    }

    /// <summary>
    /// Triggers a full sync for all mapped entities on a CRM connection
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CrmSyncResultDto>> SyncCrmConnection(long id)
    {
        var connection = await _syncService.GetConnectionAsync(id);
        if (connection == null)
            return NotFound(new { error = "Connection not found" });

        if (!connection.IsEnabled)
            return BadRequest(new { error = "Connection is disabled. Enable it before syncing." });

        // Check if there are any entity mappings
        var mappings = await _syncService.GetEntityMappingsAsync(id);
        var mappingsList = mappings.ToList();
        if (!mappingsList.Any())
            return BadRequest(new { error = "No entity mappings configured. Add entity mappings before syncing." });

        _logger.LogInformation("Starting full sync for CRM connection {ConnectionId}", id);

        // Initialize progress tracking
        var totalMappings = mappingsList.Count;
        lock (_syncProgress)
        {
            _syncProgress[id] = new SyncProgressInfo
            {
                IsRunning = true,
                Progress = 0,
                CurrentStep = 0,
                TotalSteps = totalMappings,
                Message = "Initializing sync...",
                StartedAt = DateTimeOffset.UtcNow
            };
        }

        try
        {
            // Update progress: Authenticating
            UpdateSyncProgress(id, 5, "Authenticating with CRM...", 0, totalMappings);

            // Ensure we have a valid token before syncing
            var provider = _providerFactory.GetProvider(connection);
            if (provider is Providers.DynamicsProvider dynamicsProvider)
            {
                var needsToken = string.IsNullOrEmpty(connection.AccessToken);
                var tokenExpired = connection.TokenExpiry.HasValue && connection.TokenExpiry.Value <= DateTimeOffset.UtcNow.AddMinutes(5);

                if (needsToken || tokenExpired)
                {
                    var tokenResult = await dynamicsProvider.AcquireTokenWithClientCredentialsAsync(connection);
                    if (tokenResult.Success)
                    {
                        connection.AccessToken = tokenResult.AccessToken;
                        connection.TokenExpiry = tokenResult.ExpiresAt;
                        await _syncService.UpdateConnectionAsync(connection);
                    }
                    else
                    {
                        return BadRequest(new { error = $"Failed to acquire token: {tokenResult.Error} - {tokenResult.ErrorDescription}" });
                    }
                }
            }

            // Update progress: Starting sync
            UpdateSyncProgress(id, 10, "Starting entity sync...", 0, 0);

            var result = await _syncService.SyncAllAsync(id, (entityName, currentRecord, totalRecords) =>
            {
                // Calculate progress: 10% for auth, 90% for actual sync spread across records
                var recordProgress = totalRecords > 0
                    ? 10 + (int)(currentRecord * 90.0 / totalRecords)
                    : 10;
                UpdateSyncProgress(id, recordProgress, $"Syncing {entityName}: {currentRecord}/{totalRecords}", currentRecord, totalRecords);
            });

            // Update last sync time on connection
            connection.LastSyncAt = DateTimeOffset.UtcNow;
            connection.LastSyncError = result.Success ? null : string.Join("; ", result.Errors.Take(3));
            await _syncService.UpdateConnectionAsync(connection);

            _logger.LogInformation("Completed sync for CRM connection {ConnectionId}. Success: {Success}, Processed: {Processed}, Errors: {Errors}",
                id, result.Success, result.Stats.TotalProcessed, result.Errors.Count);

            return Ok(new CrmSyncResultDto
            {
                Success = result.Success,
                Message = result.Message ?? (result.Success ? "Sync completed successfully" : "Sync completed with errors"),
                TotalProcessed = result.Stats.TotalProcessed,
                SuccessCount = result.Stats.SuccessCount,
                FailedCount = result.Stats.FailedCount,
                CreatedCount = result.Stats.CreatedCount,
                UpdatedCount = result.Stats.UpdatedCount,
                InboundCount = result.Stats.InboundCount,
                OutboundCount = result.Stats.OutboundCount,
                DurationMs = result.DurationMs,
                Errors = result.Errors.Take(10).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync for CRM connection {ConnectionId}", id);

            // Build detailed error message including inner exception
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" -> {ex.InnerException.Message}";
            }

            // Try to update connection with error, but don't fail if this fails
            try
            {
                connection.LastSyncError = errorMessage;
                await _syncService.UpdateConnectionAsync(connection);
            }
            catch (Exception updateEx)
            {
                _logger.LogWarning(updateEx, "Failed to update connection with sync error");
            }

            return StatusCode(500, new CrmSyncResultDto
            {
                Success = false,
                Message = $"Sync failed: {errorMessage}",
                Errors = new List<string> { errorMessage, ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0)) ?? "" }
            });
        }
        finally
        {
            // Clean up progress tracking
            lock (_syncProgress)
            {
                _syncProgress.Remove(id);
            }
        }
    }

    /// <summary>
    /// Gets the current sync progress for a connection
    /// </summary>
    [HttpGet]
    public ActionResult<SyncProgressInfo> GetCrmSyncProgress(long connectionId)
    {
        lock (_syncProgress)
        {
            if (_syncProgress.TryGetValue(connectionId, out var progress))
            {
                return Ok(progress);
            }
        }
        return Ok(new SyncProgressInfo { IsRunning = false, Progress = 0, Message = "No sync in progress" });
    }

    /// <summary>
    /// Gets sync statistics for a CRM connection
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CrmSyncStatsDto>> GetCrmSyncStats(long connectionId, [FromQuery] int? daysSince = 7)
    {
        var connection = await _syncService.GetConnectionAsync(connectionId);
        if (connection == null)
            return NotFound();

        var since = daysSince.HasValue ? DateTimeOffset.UtcNow.AddDays(-daysSince.Value) : (DateTimeOffset?)null;
        var stats = await _syncService.GetSyncStatsAsync(connectionId, since);

        return Ok(new CrmSyncStatsDto
        {
            TotalProcessed = stats.TotalProcessed,
            SuccessCount = stats.SuccessCount,
            FailedCount = stats.FailedCount,
            CreatedCount = stats.CreatedCount,
            UpdatedCount = stats.UpdatedCount,
            InboundCount = stats.InboundCount,
            OutboundCount = stats.OutboundCount,
            LastSyncAt = stats.LastSyncAt,
            LastSuccessfulSyncAt = stats.LastSuccessfulSyncAt
        });
    }

    /// <summary>
    /// Gets the OAuth authorization URL for a provider
    /// </summary>
    [HttpGet]
    public ActionResult<OAuthAuthorizeResponse> GetCrmOAuthAuthorizeUrl(
        [FromQuery] CrmProviderType provider,
        [FromQuery] string redirectUri,
        [FromQuery] string? state = null)
    {
        var crmProvider = _providerFactory.GetProvider(provider);
        var authUrl = crmProvider.GetAuthorizationUrl(redirectUri, state ?? Guid.NewGuid().ToString());

        return Ok(new OAuthAuthorizeResponse { AuthorizationUrl = authUrl });
    }

    /// <summary>
    /// Handles OAuth callback and exchanges code for tokens
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OAuthCallbackResponse>> HandleCrmOAuthCallback([FromBody] OAuthCallbackRequest request)
    {
        var provider = _providerFactory.GetProvider(request.Provider);
        var result = await provider.ExchangeCodeForTokenAsync(request.Code, request.RedirectUri);

        if (!result.Success)
        {
            return BadRequest(new OAuthCallbackResponse
            {
                Success = false,
                ErrorMessage = result.ErrorMessage
            });
        }

        return Ok(new OAuthCallbackResponse
        {
            Success = true,
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresAt = result.ExpiresAt
        });
    }

    /// <summary>
    /// Gets available entities from the CRM
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CrmEntitySchema>>> GetCrmAvailableEntities(long connectionId)
    {
        try
        {
            var connection = await _syncService.GetConnectionAsync(connectionId);
            if (connection == null)
            {
                _logger.LogWarning("CRM connection {ConnectionId} not found", connectionId);
                return NotFound(new { error = "Connection not found" });
            }

            // Check if connection has valid credentials (either existing token, API key, or OAuth credentials for client flow)
            var hasAccessToken = !string.IsNullOrEmpty(connection.AccessToken);
            var hasApiKey = !string.IsNullOrEmpty(connection.ApiKey);
            var hasOAuthCredentials = !string.IsNullOrEmpty(connection.ClientId) &&
                                      !string.IsNullOrEmpty(connection.ClientSecret) &&
                                      !string.IsNullOrEmpty(connection.TenantId);
            // Check both paths for appsettings credentials (AppSettings:CRM:... and CRM:...)
            var appSettingsClientId = _configuration["AppSettings:CRM:Dynamics:ClientId"] ?? _configuration["CRM:Dynamics:ClientId"];
            var appSettingsClientSecret = _configuration["AppSettings:CRM:Dynamics:ClientSecret"] ?? _configuration["CRM:Dynamics:ClientSecret"];
            var appSettingsTenantId = _configuration["AppSettings:CRM:Dynamics:TenantId"] ?? _configuration["CRM:Dynamics:TenantId"];
            var hasAppSettingsCredentials = !string.IsNullOrEmpty(appSettingsClientId) &&
                                            !string.IsNullOrEmpty(appSettingsClientSecret) &&
                                            !string.IsNullOrEmpty(appSettingsTenantId);

            _logger.LogInformation("CRM connection {ConnectionId} credential check: hasAccessToken={HasToken}, hasApiKey={HasApiKey}, hasOAuthCredentials={HasOAuth}, hasAppSettingsCredentials={HasAppSettings}",
                connectionId, hasAccessToken, hasApiKey, hasOAuthCredentials, hasAppSettingsCredentials);

            if (!hasAccessToken && !hasApiKey && !hasOAuthCredentials && !hasAppSettingsCredentials)
            {
                _logger.LogWarning("CRM connection {ConnectionId} has no credentials configured. AppSettings values - ClientId: '{ClientId}', TenantId: '{TenantId}'",
                    connectionId, appSettingsClientId ?? "(null)", appSettingsTenantId ?? "(null)");
                return BadRequest(new { error = "Connection has no credentials. Please configure ClientId, ClientSecret, and TenantId either on the connection or in appsettings (AppSettings:CRM:Dynamics:...)." });
            }

            var provider = _providerFactory.GetProvider(connection);

            // For Dynamics, acquire token first and save it (or refresh if expired)
            if (provider is Providers.DynamicsProvider dynamicsProvider)
            {
                var needsToken = string.IsNullOrEmpty(connection.AccessToken);
                var tokenExpired = connection.TokenExpiry.HasValue && connection.TokenExpiry.Value <= DateTimeOffset.UtcNow.AddMinutes(5);

                if (needsToken || tokenExpired)
                {
                    _logger.LogInformation("Acquiring new token for connection {ConnectionId} (needsToken: {NeedsToken}, expired: {Expired})",
                        connectionId, needsToken, tokenExpired);

                    var tokenResult = await dynamicsProvider.AcquireTokenWithClientCredentialsAsync(connection);
                    if (tokenResult.Success)
                    {
                        connection.AccessToken = tokenResult.AccessToken;
                        connection.TokenExpiry = tokenResult.ExpiresAt;
                        // Save the token to the database
                        await _syncService.UpdateConnectionAsync(connection);
                        _logger.LogInformation("Acquired and saved OAuth token for connection {ConnectionId}", connectionId);
                    }
                    else
                    {
                        _logger.LogWarning("CRM connection {ConnectionId} token acquisition failed: {Error}", connectionId, tokenResult.ErrorDescription);
                        return BadRequest(new { error = $"Failed to acquire token: {tokenResult.Error} - {tokenResult.ErrorDescription}" });
                    }
                }
            }

            // Validate the connection
            var isValid = await provider.ValidateConnectionAsync(connection);
            if (!isValid)
            {
                _logger.LogWarning("CRM connection {ConnectionId} validation failed", connectionId);
                return BadRequest(new { error = "Connection validation failed. Check your ClientId, ClientSecret, TenantId, and Environment URL are correct." });
            }

            var entities = await provider.GetAvailableEntitiesAsync(connection);
            _logger.LogInformation("Retrieved {Count} entities from CRM connection {ConnectionId}",
                entities?.Count() ?? 0, connectionId);

            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CRM entities for connection {ConnectionId}", connectionId);
            return StatusCode(500, new {
                error = ex.Message,
                details = ex.InnerException?.Message,
                exceptionType = ex.GetType().Name,
                stackTrace = ex.StackTrace?.Substring(0, Math.Min(1000, ex.StackTrace?.Length ?? 0))
            });
        }
    }

    /// <summary>
    /// Gets fields for a specific CRM entity
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CrmFieldSchema>>> GetCrmEntityFields(long connectionId, string entityName)
    {
        try
        {
            var connection = await _syncService.GetConnectionAsync(connectionId);
            if (connection == null)
            {
                _logger.LogWarning("CRM connection {ConnectionId} not found", connectionId);
                return NotFound(new { error = "Connection not found" });
            }

            // Check if connection has valid credentials
            var hasAccessToken = !string.IsNullOrEmpty(connection.AccessToken);
            var hasApiKey = !string.IsNullOrEmpty(connection.ApiKey);
            var hasOAuthCredentials = !string.IsNullOrEmpty(connection.ClientId) &&
                                      !string.IsNullOrEmpty(connection.ClientSecret) &&
                                      !string.IsNullOrEmpty(connection.TenantId);
            // Check both paths for appsettings credentials (AppSettings:CRM:... and CRM:...)
            var hasAppSettingsCredentials = !string.IsNullOrEmpty(_configuration["AppSettings:CRM:Dynamics:ClientId"] ?? _configuration["CRM:Dynamics:ClientId"]) &&
                                            !string.IsNullOrEmpty(_configuration["AppSettings:CRM:Dynamics:ClientSecret"] ?? _configuration["CRM:Dynamics:ClientSecret"]) &&
                                            !string.IsNullOrEmpty(_configuration["AppSettings:CRM:Dynamics:TenantId"] ?? _configuration["CRM:Dynamics:TenantId"]);

            if (!hasAccessToken && !hasApiKey && !hasOAuthCredentials && !hasAppSettingsCredentials)
            {
                _logger.LogWarning("CRM connection {ConnectionId} has no credentials configured", connectionId);
                return BadRequest(new { error = "Connection has no credentials configured." });
            }

            var provider = _providerFactory.GetProvider(connection);

            // For Dynamics, acquire token first and save it (or refresh if expired)
            if (provider is Providers.DynamicsProvider dynamicsProvider)
            {
                var needsToken = string.IsNullOrEmpty(connection.AccessToken);
                var tokenExpired = connection.TokenExpiry.HasValue && connection.TokenExpiry.Value <= DateTimeOffset.UtcNow.AddMinutes(5);

                if (needsToken || tokenExpired)
                {
                    _logger.LogInformation("Acquiring new token for connection {ConnectionId} (needsToken: {NeedsToken}, expired: {Expired})",
                        connectionId, needsToken, tokenExpired);

                    var tokenResult = await dynamicsProvider.AcquireTokenWithClientCredentialsAsync(connection);
                    if (tokenResult.Success)
                    {
                        connection.AccessToken = tokenResult.AccessToken;
                        connection.TokenExpiry = tokenResult.ExpiresAt;
                        // Save the token to the database so subsequent calls don't need to re-authenticate
                        await _syncService.UpdateConnectionAsync(connection);
                    }
                    else
                    {
                        return BadRequest(new { error = $"Failed to acquire token: {tokenResult.Error} - {tokenResult.ErrorDescription}" });
                    }
                }
            }

            _logger.LogInformation("Fetching fields for entity {EntityName}. Connection has token: {HasToken}, TokenExpiry: {TokenExpiry}",
                entityName, !string.IsNullOrEmpty(connection.AccessToken), connection.TokenExpiry);

            var fields = await provider.GetEntityFieldsAsync(connection, entityName);
            _logger.LogInformation("Retrieved {Count} fields for entity {EntityName} from CRM connection {ConnectionId}",
                fields?.Count() ?? 0, entityName, connectionId);

            return Ok(fields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CRM fields for entity {EntityName} on connection {ConnectionId}", entityName, connectionId);
            return StatusCode(500, new {
                error = ex.Message,
                details = ex.InnerException?.Message,
                exceptionType = ex.GetType().Name
            });
        }
    }

    #region Private Helpers

    private void UpdateSyncProgress(long connectionId, int progress, string message, int currentStep, int totalSteps)
    {
        lock (_syncProgress)
        {
            if (_syncProgress.TryGetValue(connectionId, out var info))
            {
                info.Progress = progress;
                info.Message = message;
                info.CurrentStep = currentStep;
                info.TotalSteps = totalSteps;
            }
        }
    }

    private static CrmConnectionDto MapToDto(CrmConnection connection)
    {
        return new CrmConnectionDto
        {
            Id = connection.Id,
            CompanyId = connection.CompanyId,
            Provider = connection.Provider,
            DisplayName = connection.DisplayName,
            EnvironmentUrl = connection.EnvironmentUrl,
            SyncDirection = connection.SyncDirection,
            SyncIntervalMinutes = connection.SyncIntervalMinutes,
            IsEnabled = connection.IsEnabled,
            LastSyncAt = connection.LastSyncAt,
            Created = connection.Created,
            Updated = connection.Updated,
            HasAccessToken = !string.IsNullOrEmpty(connection.AccessToken),
            HasApiKey = !string.IsNullOrEmpty(connection.ApiKey)
        };
    }

    #endregion
}

#region DTOs

public class CrmConnectionDto
{
    public long Id { get; set; }
    public long? CompanyId { get; set; }
    public CrmProviderType Provider { get; set; }
    public string? DisplayName { get; set; }
    public string? EnvironmentUrl { get; set; }
    public CrmSyncDirection SyncDirection { get; set; }
    public int SyncIntervalMinutes { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Updated { get; set; }
    public bool HasAccessToken { get; set; }
    public bool HasApiKey { get; set; }
}

public class CrmProviderInfo
{
    public CrmProviderType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool SupportsOAuth { get; set; }
    public bool SupportsApiKey { get; set; }
}

public class CreateCrmConnectionRequest
{
    public long? CompanyId { get; set; }
    public CrmProviderType Provider { get; set; }
    public string? DisplayName { get; set; }
    public string? EnvironmentUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TenantId { get; set; }
    public CrmSyncDirection SyncDirection { get; set; } = CrmSyncDirection.Bidirectional;
    public int? SyncIntervalMinutes { get; set; }
    public bool? IsEnabled { get; set; }
}

public class UpdateCrmConnectionRequest
{
    public string? DisplayName { get; set; }
    public string? EnvironmentUrl { get; set; }
    public CrmSyncDirection? SyncDirection { get; set; }
    public int? SyncIntervalMinutes { get; set; }
    public bool? IsEnabled { get; set; }

    // OAuth credentials (for updating existing connections)
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TenantId { get; set; }
    public string? ApiKey { get; set; }
}

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class OAuthAuthorizeResponse
{
    public string AuthorizationUrl { get; set; } = string.Empty;
}

public class OAuthCallbackRequest
{
    public CrmProviderType Provider { get; set; }
    public string Code { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}

public class OAuthCallbackResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CrmSyncResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int InboundCount { get; set; }
    public int OutboundCount { get; set; }
    public long DurationMs { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class CrmSyncStatsDto
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int InboundCount { get; set; }
    public int OutboundCount { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }
}

public class SyncProgressInfo
{
    public bool IsRunning { get; set; }
    public int Progress { get; set; }
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; set; }
}

#endregion
