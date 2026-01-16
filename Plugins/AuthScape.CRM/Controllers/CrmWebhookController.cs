using AuthScape.CRM.Interfaces;
using AuthScape.CRM.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AuthScape.CRM.Controllers;

/// <summary>
/// API controller for handling CRM webhook events
/// </summary>
[ApiController]
[Route("api/crm/webhook")]
public class CrmWebhookController : ControllerBase
{
    private readonly ICrmSyncService _syncService;
    private readonly ICrmProviderFactory _providerFactory;
    private readonly ILogger<CrmWebhookController> _logger;

    public CrmWebhookController(
        ICrmSyncService syncService,
        ICrmProviderFactory providerFactory,
        ILogger<CrmWebhookController> logger)
    {
        _syncService = syncService;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles webhook events from Microsoft Dynamics 365
    /// </summary>
    [HttpPost("dynamics/{connectionId}")]
    public async Task<IActionResult> HandleDynamicsWebhook(long connectionId)
    {
        return await HandleWebhook(connectionId, CrmProviderType.Dynamics365);
    }

    /// <summary>
    /// Handles webhook events from HubSpot
    /// </summary>
    [HttpPost("hubspot/{connectionId}")]
    public async Task<IActionResult> HandleHubSpotWebhook(long connectionId)
    {
        return await HandleWebhook(connectionId, CrmProviderType.HubSpot);
    }

    /// <summary>
    /// Handles webhook events from Google Contacts
    /// </summary>
    [HttpPost("google/{connectionId}")]
    public async Task<IActionResult> HandleGoogleWebhook(long connectionId)
    {
        return await HandleWebhook(connectionId, CrmProviderType.GoogleContacts);
    }

    /// <summary>
    /// Handles webhook events from Salesforce
    /// </summary>
    [HttpPost("salesforce/{connectionId}")]
    public async Task<IActionResult> HandleSalesforceWebhook(long connectionId)
    {
        return await HandleWebhook(connectionId, CrmProviderType.Salesforce);
    }

    /// <summary>
    /// Generic webhook endpoint that accepts provider type as parameter
    /// </summary>
    [HttpPost("{provider}/{connectionId}")]
    public async Task<IActionResult> HandleGenericWebhook(string provider, long connectionId)
    {
        if (!Enum.TryParse<CrmProviderType>(provider, true, out var providerType))
        {
            _logger.LogWarning("Unknown CRM provider type: {Provider}", provider);
            return BadRequest($"Unknown provider: {provider}");
        }

        return await HandleWebhook(connectionId, providerType);
    }

    /// <summary>
    /// Registers a webhook endpoint with the CRM provider
    /// </summary>
    [HttpPost("{connectionId}/register")]
    public async Task<ActionResult<WebhookRegistrationResult>> RegisterWebhook(
        long connectionId,
        [FromBody] WebhookRegistrationRequest request)
    {
        var connection = await _syncService.GetConnectionAsync(connectionId);
        if (connection == null)
            return NotFound();

        var provider = _providerFactory.GetProvider(connection);

        // Build the webhook URL based on the provider
        var webhookUrl = $"{request.BaseUrl}/api/crm/webhook/{connection.Provider.ToString().ToLowerInvariant()}/{connectionId}";

        var success = await provider.RegisterWebhookAsync(connection, webhookUrl, request.EntityNames);

        return Ok(new WebhookRegistrationResult
        {
            Success = success,
            WebhookUrl = webhookUrl,
            Message = success ? "Webhook registered successfully" : "Failed to register webhook"
        });
    }

    #region Private Methods

    private async Task<IActionResult> HandleWebhook(long connectionId, CrmProviderType expectedProvider)
    {
        try
        {
            // Read the raw payload
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();

            _logger.LogInformation("Received {Provider} webhook for connection {ConnectionId}",
                expectedProvider, connectionId);

            // Get the connection
            var connection = await _syncService.GetConnectionAsync(connectionId);
            if (connection == null)
            {
                _logger.LogWarning("Connection {ConnectionId} not found for webhook", connectionId);
                return NotFound();
            }

            // Verify the provider matches
            if (connection.Provider != expectedProvider)
            {
                _logger.LogWarning("Provider mismatch: expected {Expected}, got {Actual}",
                    expectedProvider, connection.Provider);
                return BadRequest("Provider mismatch");
            }

            // Check if connection is enabled
            if (!connection.IsEnabled)
            {
                _logger.LogInformation("Connection {ConnectionId} is disabled, ignoring webhook", connectionId);
                return Ok();
            }

            // Get headers for signature validation
            var headers = Request.Headers.ToDictionary(
                h => h.Key,
                h => h.Value.ToString());

            // Get the provider and validate signature
            var provider = _providerFactory.GetProvider(connection);

            // Validate webhook signature if webhook secret is configured
            if (!string.IsNullOrEmpty(connection.WebhookSecret))
            {
                if (!provider.ValidateWebhookSignature(payload, headers, connection.WebhookSecret))
                {
                    _logger.LogWarning("Invalid webhook signature for connection {ConnectionId}", connectionId);
                    return Unauthorized("Invalid signature");
                }
            }

            // Parse the webhook payload
            var webhookEvent = await provider.ParseWebhookPayloadAsync(payload, headers);
            if (webhookEvent == null)
            {
                _logger.LogWarning("Failed to parse webhook payload for connection {ConnectionId}", connectionId);
                return BadRequest("Failed to parse webhook payload");
            }

            _logger.LogInformation("Processing {EventType} event for {EntityName} {RecordId}",
                webhookEvent.EventType, webhookEvent.EntityName, webhookEvent.RecordId);

            // Process the webhook event based on event type
            switch (webhookEvent.EventType.ToLowerInvariant())
            {
                case "create":
                case "created":
                    await _syncService.SyncInboundAsync(connectionId, webhookEvent.EntityName, webhookEvent.RecordId);
                    break;

                case "update":
                case "updated":
                case "modify":
                case "modified":
                    await _syncService.SyncInboundAsync(connectionId, webhookEvent.EntityName, webhookEvent.RecordId);
                    break;

                case "delete":
                case "deleted":
                    // Handle delete - mark as deleted or remove link
                    _logger.LogInformation("Record {RecordId} deleted in CRM", webhookEvent.RecordId);
                    // TODO: Implement delete handling based on configuration
                    break;

                default:
                    _logger.LogInformation("Unhandled webhook event type: {EventType}", webhookEvent.EventType);
                    break;
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook for connection {ConnectionId}", connectionId);
            // Return 200 to prevent retries for unrecoverable errors
            return Ok();
        }
    }

    #endregion
}

#region DTOs

public class WebhookRegistrationRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public List<string> EntityNames { get; set; } = new();
}

public class WebhookRegistrationResult
{
    public bool Success { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string? Message { get; set; }
}

#endregion
