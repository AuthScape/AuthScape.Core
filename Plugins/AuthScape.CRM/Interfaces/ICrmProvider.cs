using AuthScape.CRM.Models;

namespace AuthScape.CRM.Interfaces;

/// <summary>
/// Interface for CRM providers. Each CRM platform (Dynamics, HubSpot, etc.)
/// implements this interface to provide a consistent API for sync operations.
/// </summary>
public interface ICrmProvider
{
    /// <summary>
    /// The name of this provider (e.g., "Dynamics365", "HubSpot")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Validates that the connection credentials are valid and the CRM is reachable
    /// </summary>
    Task<bool> ValidateConnectionAsync(CrmConnection connection);

    /// <summary>
    /// Refreshes the OAuth access token using the refresh token
    /// </summary>
    Task<CrmAuthResult> RefreshTokenAsync(CrmConnection connection);

    /// <summary>
    /// Gets the OAuth authorization URL for the initial authentication flow
    /// </summary>
    string GetAuthorizationUrl(string redirectUri, string state);

    /// <summary>
    /// Exchanges an authorization code for access/refresh tokens
    /// </summary>
    Task<CrmAuthResult> ExchangeCodeForTokenAsync(string code, string redirectUri);

    #region Schema Discovery

    /// <summary>
    /// Gets a list of available entities from the CRM that can be synced
    /// </summary>
    Task<IEnumerable<CrmEntitySchema>> GetAvailableEntitiesAsync(CrmConnection connection);

    /// <summary>
    /// Gets the fields/attributes of a specific entity
    /// </summary>
    Task<IEnumerable<CrmFieldSchema>> GetEntityFieldsAsync(CrmConnection connection, string entityName);

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Gets a single record by ID
    /// </summary>
    Task<CrmRecord?> GetRecordAsync(CrmConnection connection, string entityName, string recordId);

    /// <summary>
    /// Gets records from the CRM, optionally filtered by modification date
    /// </summary>
    /// <param name="connection">The CRM connection</param>
    /// <param name="entityName">The entity to query</param>
    /// <param name="modifiedSince">Only return records modified after this date (for incremental sync)</param>
    /// <param name="filter">Optional provider-specific filter expression</param>
    /// <param name="top">Maximum number of records to return</param>
    Task<IEnumerable<CrmRecord>> GetRecordsAsync(
        CrmConnection connection,
        string entityName,
        DateTimeOffset? modifiedSince = null,
        string? filter = null,
        int? top = null);

    /// <summary>
    /// Creates a new record in the CRM
    /// </summary>
    /// <returns>The ID of the created record</returns>
    Task<string> CreateRecordAsync(CrmConnection connection, string entityName, Dictionary<string, object?> fields);

    /// <summary>
    /// Updates an existing record in the CRM
    /// </summary>
    Task UpdateRecordAsync(CrmConnection connection, string entityName, string recordId, Dictionary<string, object?> fields);

    /// <summary>
    /// Deletes a record from the CRM
    /// </summary>
    Task DeleteRecordAsync(CrmConnection connection, string entityName, string recordId);

    #endregion

    #region Webhooks

    /// <summary>
    /// Registers a webhook endpoint to receive real-time updates from the CRM
    /// </summary>
    /// <param name="connection">The CRM connection</param>
    /// <param name="webhookUrl">The URL to receive webhook callbacks</param>
    /// <param name="entityNames">The entities to subscribe to</param>
    /// <returns>True if registration was successful</returns>
    Task<bool> RegisterWebhookAsync(CrmConnection connection, string webhookUrl, IEnumerable<string> entityNames);

    /// <summary>
    /// Parses an incoming webhook payload into a structured event
    /// </summary>
    /// <param name="payload">The raw webhook payload body</param>
    /// <param name="headers">The HTTP headers from the webhook request</param>
    /// <returns>The parsed webhook event</returns>
    Task<CrmWebhookEvent?> ParseWebhookPayloadAsync(string payload, IDictionary<string, string> headers);

    /// <summary>
    /// Validates that a webhook request is authentic (signature verification)
    /// </summary>
    bool ValidateWebhookSignature(string payload, IDictionary<string, string> headers, string? secret);

    #endregion
}
