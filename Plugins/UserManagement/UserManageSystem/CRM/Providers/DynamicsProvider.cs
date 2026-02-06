using System.Net.Http.Headers;
using System.Text;
using System.Web;
using AuthScape.UserManageSystem.Models.CRM;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AuthScape.UserManageSystem.CRM.Providers;

/// <summary>
/// CRM provider for Microsoft Dynamics 365 / Dataverse
/// Uses the Dataverse Web API: https://docs.microsoft.com/en-us/power-apps/developer/data-platform/webapi/overview
/// </summary>
public class DynamicsProvider : BaseCrmProvider
{
    private const string API_VERSION = "v9.2";
    private const string AZURE_AD_AUTHORITY = "https://login.microsoftonline.com";

    // These should come from configuration
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _tenantId;

    public override string ProviderName => "Dynamics365";

    public DynamicsProvider(
        string clientId,
        string clientSecret,
        string tenantId,
        IHttpClientFactory? httpClientFactory = null,
        ILogger<DynamicsProvider>? logger = null) : base(httpClientFactory, logger)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tenantId = tenantId;
    }

    #region Authentication

    public override string GetAuthorizationUrl(string redirectUri, string state)
    {
        var scope = "https://org.crm.dynamics.com/.default offline_access";
        return $"{AZURE_AD_AUTHORITY}/{_tenantId}/oauth2/v2.0/authorize?" +
               $"client_id={_clientId}&" +
               $"response_type=code&" +
               $"redirect_uri={HttpUtility.UrlEncode(redirectUri)}&" +
               $"scope={HttpUtility.UrlEncode(scope)}&" +
               $"state={state}";
    }

    public override async Task<CrmAuthResult> ExchangeCodeForTokenAsync(string code, string redirectUri)
    {
        try
        {
            var tokenUrl = $"{AZURE_AD_AUTHORITY}/{_tenantId}/oauth2/v2.0/token";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("scope", "https://org.crm.dynamics.com/.default offline_access")
            });

            var response = await _httpClient.PostAsync(tokenUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = JObject.Parse(responseContent);
                return new CrmAuthResult
                {
                    Success = false,
                    Error = error["error"]?.ToString(),
                    ErrorDescription = error["error_description"]?.ToString()
                };
            }

            var tokenResponse = JObject.Parse(responseContent);
            var expiresIn = tokenResponse["expires_in"]?.Value<int>() ?? 3600;

            return new CrmAuthResult
            {
                Success = true,
                AccessToken = tokenResponse["access_token"]?.ToString(),
                RefreshToken = tokenResponse["refresh_token"]?.ToString(),
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
            };
        }
        catch (Exception ex)
        {
            return new CrmAuthResult
            {
                Success = false,
                Error = "token_exchange_failed",
                ErrorDescription = ex.Message
            };
        }
    }

    public override async Task<CrmAuthResult> RefreshTokenAsync(CrmConnection connection)
    {
        try
        {
            if (string.IsNullOrEmpty(connection.RefreshToken))
            {
                return new CrmAuthResult { Success = false, Error = "no_refresh_token" };
            }

            var tokenUrl = $"{AZURE_AD_AUTHORITY}/{_tenantId}/oauth2/v2.0/token";
            var resourceUrl = GetResourceUrl(connection);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("refresh_token", connection.RefreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("scope", $"{resourceUrl}/.default offline_access")
            });

            var response = await _httpClient.PostAsync(tokenUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = JObject.Parse(responseContent);
                return new CrmAuthResult
                {
                    Success = false,
                    Error = error["error"]?.ToString(),
                    ErrorDescription = error["error_description"]?.ToString()
                };
            }

            var tokenResponse = JObject.Parse(responseContent);
            var expiresIn = tokenResponse["expires_in"]?.Value<int>() ?? 3600;

            return new CrmAuthResult
            {
                Success = true,
                AccessToken = tokenResponse["access_token"]?.ToString(),
                RefreshToken = tokenResponse["refresh_token"]?.ToString() ?? connection.RefreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
            };
        }
        catch (Exception ex)
        {
            return new CrmAuthResult
            {
                Success = false,
                Error = "refresh_failed",
                ErrorDescription = ex.Message
            };
        }
    }

    public override async Task<bool> ValidateConnectionAsync(CrmConnection connection)
    {
        try
        {
            // Ensure we have a valid token (will use client credentials if no token exists)
            connection = await EnsureValidTokenAsync(connection);

            var url = $"{GetApiBaseUrl(connection)}/WhoAmI";
            await GetAsync<JObject>(connection, url);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Acquires a token using client credentials flow (server-to-server auth).
    /// This is used when no access token exists and connection has ClientId/ClientSecret.
    /// </summary>
    public async Task<CrmAuthResult> AcquireTokenWithClientCredentialsAsync(CrmConnection connection)
    {
        try
        {
            // Use connection-level credentials if available, otherwise fall back to factory-level
            var clientId = !string.IsNullOrEmpty(connection.ClientId) ? connection.ClientId : _clientId;
            var clientSecret = !string.IsNullOrEmpty(connection.ClientSecret) ? connection.ClientSecret : _clientSecret;
            var tenantId = !string.IsNullOrEmpty(connection.TenantId) ? connection.TenantId : _tenantId;

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(tenantId))
            {
                return new CrmAuthResult
                {
                    Success = false,
                    Error = "missing_credentials",
                    ErrorDescription = $"ClientId, ClientSecret, and TenantId are required. Have ClientId: {!string.IsNullOrEmpty(clientId)}, ClientSecret: {!string.IsNullOrEmpty(clientSecret)}, TenantId: {!string.IsNullOrEmpty(tenantId)}"
                };
            }

            var tokenUrl = $"{AZURE_AD_AUTHORITY}/{tenantId}/oauth2/v2.0/token";
            var resourceUrl = GetResourceUrl(connection);
            var scope = $"{resourceUrl}/.default";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", scope)
            });

            var response = await _httpClient.PostAsync(tokenUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Try to parse as JSON
            JObject? jsonResponse = null;
            try
            {
                jsonResponse = JObject.Parse(responseContent);
            }
            catch
            {
                // Response is not valid JSON
                return new CrmAuthResult
                {
                    Success = false,
                    Error = "invalid_response",
                    ErrorDescription = $"Token endpoint returned non-JSON response (HTTP {(int)response.StatusCode}): {responseContent.Substring(0, Math.Min(500, responseContent.Length))}"
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorCode = jsonResponse["error"]?.ToString() ?? "unknown_error";
                var errorDesc = jsonResponse["error_description"]?.ToString() ?? responseContent;
                return new CrmAuthResult
                {
                    Success = false,
                    Error = errorCode,
                    ErrorDescription = $"{errorDesc} (Scope: {scope}, TenantId: {tenantId})"
                };
            }

            // Verify we got an access token
            var accessToken = jsonResponse["access_token"]?.ToString();
            if (string.IsNullOrEmpty(accessToken))
            {
                return new CrmAuthResult
                {
                    Success = false,
                    Error = "no_token",
                    ErrorDescription = $"Token response did not contain access_token. Response: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}"
                };
            }

            var expiresIn = 3600;
            var expiresInToken = jsonResponse["expires_in"];
            if (expiresInToken != null && expiresInToken.Type == JTokenType.Integer)
            {
                expiresIn = expiresInToken.Value<int>();
            }

            return new CrmAuthResult
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = null, // Client credentials flow doesn't return refresh token
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
            };
        }
        catch (Exception ex)
        {
            return new CrmAuthResult
            {
                Success = false,
                Error = "client_credentials_failed",
                ErrorDescription = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Override to support client credentials flow when no token exists
    /// </summary>
    protected new async Task<CrmConnection> EnsureValidTokenAsync(CrmConnection connection)
    {
        // If we have no token at all, try to acquire one using client credentials
        if (string.IsNullOrEmpty(connection.AccessToken))
        {
            var result = await AcquireTokenWithClientCredentialsAsync(connection);
            if (result.Success)
            {
                connection.AccessToken = result.AccessToken;
                connection.TokenExpiry = result.ExpiresAt;
            }
            return connection;
        }

        // If token is expired and we have refresh token, use that
        if (IsTokenExpired(connection) && !string.IsNullOrEmpty(connection.RefreshToken))
        {
            var result = await RefreshTokenAsync(connection);
            if (result.Success)
            {
                connection.AccessToken = result.AccessToken;
                connection.RefreshToken = result.RefreshToken;
                connection.TokenExpiry = result.ExpiresAt;
            }
            return connection;
        }

        // If token is expired but no refresh token, try client credentials again
        if (IsTokenExpired(connection))
        {
            var result = await AcquireTokenWithClientCredentialsAsync(connection);
            if (result.Success)
            {
                connection.AccessToken = result.AccessToken;
                connection.TokenExpiry = result.ExpiresAt;
            }
        }

        return connection;
    }

    #endregion

    #region Schema Discovery

    public override async Task<IEnumerable<CrmEntitySchema>> GetAvailableEntitiesAsync(CrmConnection connection)
    {
        connection = await EnsureValidTokenAsync(connection);

        var url = $"{GetApiBaseUrl(connection)}/EntityDefinitions?" +
                  "$select=LogicalName,DisplayName,DisplayCollectionName,Description,IsCustomEntity,PrimaryIdAttribute,PrimaryNameAttribute,EntitySetName&" +
                  "$filter=IsValidForAdvancedFind eq true";

        var response = await GetAsync<JObject>(connection, url);
        var entities = new List<CrmEntitySchema>();

        if (response?["value"] is JArray entityArray)
        {
            foreach (var entity in entityArray)
            {
                try
                {
                    var logicalName = entity["LogicalName"]?.ToString() ?? "";
                    entities.Add(new CrmEntitySchema
                    {
                        LogicalName = logicalName,
                        DisplayName = GetLocalizedLabel(entity["DisplayName"]) ?? logicalName,
                        PluralName = GetLocalizedLabel(entity["DisplayCollectionName"]),
                        Description = GetLocalizedLabel(entity["Description"]),
                        IsCustomEntity = GetBoolValue(entity["IsCustomEntity"]),
                        PrimaryKeyField = entity["PrimaryIdAttribute"]?.ToString() ?? "id",
                        PrimaryNameField = entity["PrimaryNameAttribute"]?.ToString(),
                        CollectionName = entity["EntitySetName"]?.ToString()
                    });
                }
                catch
                {
                    // Skip entities that fail to parse
                    continue;
                }
            }
        }

        return entities.OrderBy(e => e.DisplayName);
    }

    /// <summary>
    /// Safely extracts a localized label from a Dynamics label object
    /// </summary>
    private string? GetLocalizedLabel(JToken? token)
    {
        try
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            // If it's a simple string value, return it directly
            if (token.Type == JTokenType.String)
                return token.ToString();

            // If it's an object, try to get UserLocalizedLabel.Label
            if (token.Type == JTokenType.Object)
            {
                var userLocalizedLabel = token["UserLocalizedLabel"];

                // UserLocalizedLabel could be null, a JValue (null), or a JObject
                if (userLocalizedLabel != null && userLocalizedLabel.Type == JTokenType.Object)
                {
                    var label = userLocalizedLabel["Label"]?.ToString();
                    if (!string.IsNullOrEmpty(label))
                        return label;
                }

                // Fallback to LocalizedLabels array
                var localizedLabels = token["LocalizedLabels"];
                if (localizedLabels != null && localizedLabels.Type == JTokenType.Array)
                {
                    var labelsArray = localizedLabels as JArray;
                    if (labelsArray != null && labelsArray.Count > 0)
                    {
                        var firstLabel = labelsArray[0];
                        if (firstLabel != null && firstLabel.Type == JTokenType.Object)
                        {
                            return firstLabel["Label"]?.ToString();
                        }
                    }
                }
            }

            return null;
        }
        catch
        {
            // If anything goes wrong during parsing, return null safely
            return null;
        }
    }

    /// <summary>
    /// Safely extracts a boolean value from a JToken
    /// </summary>
    private bool GetBoolValue(JToken? token)
    {
        try
        {
            if (token == null || token.Type == JTokenType.Null)
                return false;
            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();
            if (token.Type == JTokenType.String)
                return bool.TryParse(token.ToString(), out var result) && result;
            return false;
        }
        catch
        {
            return false;
        }
    }

    public override async Task<IEnumerable<CrmFieldSchema>> GetEntityFieldsAsync(CrmConnection connection, string entityName)
    {
        // Log before token check
        var hadTokenBefore = !string.IsNullOrEmpty(connection.AccessToken);

        connection = await EnsureValidTokenAsync(connection);

        // Log after token check
        var hasTokenAfter = !string.IsNullOrEmpty(connection.AccessToken);

        // First, get all attributes (without Targets which is only on LookupAttributeMetadata)
        var url = $"{GetApiBaseUrl(connection)}/EntityDefinitions(LogicalName='{entityName}')/Attributes?" +
                  "$select=LogicalName,DisplayName,Description,AttributeType,RequiredLevel,IsCustomAttribute";

        // Log the URL being called (without the token for security)
        System.Diagnostics.Debug.WriteLine($"GetEntityFieldsAsync: Entity={entityName}, HadToken={hadTokenBefore}, HasToken={hasTokenAfter}, URL={url}");

        var response = await GetAsync<JObject>(connection, url);
        var fields = new List<CrmFieldSchema>();

        // Also get lookup attributes with their Targets
        var lookupUrl = $"{GetApiBaseUrl(connection)}/EntityDefinitions(LogicalName='{entityName}')/Attributes/Microsoft.Dynamics.CRM.LookupAttributeMetadata?" +
                        "$select=LogicalName,Targets";
        var lookupResponse = await GetAsync<JObject>(connection, lookupUrl);
        var lookupTargets = new Dictionary<string, string>();

        if (lookupResponse?["value"] is JArray lookupArray)
        {
            foreach (var lookup in lookupArray)
            {
                var logicalName = lookup["LogicalName"]?.ToString();
                var targets = lookup["Targets"] as JArray;
                if (!string.IsNullOrEmpty(logicalName) && targets != null && targets.Count > 0)
                {
                    lookupTargets[logicalName] = targets[0]?.ToString() ?? "";
                }
            }
        }

        if (response?["value"] is JArray fieldArray)
        {
            foreach (var field in fieldArray)
            {
                try
                {
                    var attributeType = field["AttributeType"]?.ToString() ?? "String";

                    // Skip internal/system fields
                    var logicalName = field["LogicalName"]?.ToString() ?? "";
                    if (logicalName.StartsWith("yomi") || logicalName.EndsWith("_base"))
                        continue;

                    // Get related entity for lookup fields from our lookup query
                    string? relatedEntityName = null;
                    if (attributeType == "Lookup" || attributeType == "Customer" || attributeType == "Owner")
                    {
                        lookupTargets.TryGetValue(logicalName, out relatedEntityName);
                    }

                    fields.Add(new CrmFieldSchema
                    {
                        LogicalName = logicalName,
                        DisplayName = GetLocalizedLabel(field["DisplayName"]) ?? logicalName,
                        Description = GetLocalizedLabel(field["Description"]),
                        DataType = MapDynamicsAttributeType(attributeType),
                        IsRequired = GetRequiredLevel(field["RequiredLevel"]),
                        IsReadOnly = attributeType == "Uniqueidentifier" || attributeType == "EntityName",
                        IsCustomField = GetBoolValue(field["IsCustomAttribute"]),
                        MaxLength = GetIntValue(field["MaxLength"]),
                        RelatedEntityName = relatedEntityName
                    });
                }
                catch
                {
                    // Skip fields that fail to parse
                    continue;
                }
            }
        }

        return fields.OrderBy(f => f.DisplayName);
    }

    /// <summary>
    /// Safely extracts the required level from a Dynamics RequiredLevel object
    /// </summary>
    private bool GetRequiredLevel(JToken? token)
    {
        try
        {
            if (token == null || token.Type == JTokenType.Null)
                return false;
            if (token.Type == JTokenType.String)
                return token.ToString() == "ApplicationRequired";
            if (token.Type == JTokenType.Object)
            {
                var valueToken = token["Value"];
                if (valueToken != null && (valueToken.Type == JTokenType.String || valueToken.Type == JTokenType.Integer))
                    return valueToken.ToString() == "ApplicationRequired";
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Safely extracts an integer value from a JToken
    /// </summary>
    private int? GetIntValue(JToken? token)
    {
        try
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;
            if (token.Type == JTokenType.Integer)
                return token.Value<int>();
            if (token.Type == JTokenType.String && int.TryParse(token.ToString(), out var result))
                return result;
            return null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region CRUD Operations

    public override async Task<CrmRecord?> GetRecordAsync(CrmConnection connection, string entityName, string recordId)
    {
        connection = await EnsureValidTokenAsync(connection);

        var collectionName = await GetCollectionNameAsync(connection, entityName);
        var url = $"{GetApiBaseUrl(connection)}/{collectionName}({recordId})";

        try
        {
            var response = await GetAsync<JObject>(connection, url);
            if (response == null) return null;

            return ParseCrmRecord(entityName, response);
        }
        catch
        {
            return null;
        }
    }

    public override async Task<IEnumerable<CrmRecord>> GetRecordsAsync(
        CrmConnection connection,
        string entityName,
        DateTimeOffset? modifiedSince = null,
        string? filter = null,
        int? top = null)
    {
        connection = await EnsureValidTokenAsync(connection);

        var collectionName = await GetCollectionNameAsync(connection, entityName);
        var url = $"{GetApiBaseUrl(connection)}/{collectionName}";

        var queryParams = new List<string>();
        var filterParts = new List<string>();

        // Add modifiedSince filter if provided
        if (modifiedSince.HasValue)
        {
            var filterDate = modifiedSince.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
            filterParts.Add($"modifiedon ge {filterDate}");
        }

        // Add custom filter if provided (can be combined with modifiedSince)
        if (!string.IsNullOrEmpty(filter))
        {
            filterParts.Add($"({filter})");
        }

        // Combine filter parts with 'and'
        if (filterParts.Count > 0)
        {
            queryParams.Add($"$filter={string.Join(" and ", filterParts)}");
        }

        if (top.HasValue)
        {
            queryParams.Add($"$top={top.Value}");
        }

        queryParams.Add("$orderby=modifiedon desc");

        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }

        System.Diagnostics.Debug.WriteLine($"GetRecordsAsync: URL={url}");

        var response = await GetAsync<JObject>(connection, url);
        var records = new List<CrmRecord>();

        if (response?["value"] is JArray recordArray)
        {
            foreach (var record in recordArray)
            {
                if (record is JObject recordObj)
                {
                    records.Add(ParseCrmRecord(entityName, recordObj));
                }
            }
        }

        return records;
    }

    /// <summary>
    /// Removes read-only primary key fields that Dynamics 365 does not allow in create/update payloads.
    /// For example, 'accountid' cannot be sent on a contact entity, and 'contactid' cannot be sent on a contact create.
    /// </summary>
    private Dictionary<string, object?> SanitizeFieldsForWrite(string entityName, Dictionary<string, object?> fields)
    {
        // Primary key fields that are auto-generated by Dynamics and must never be included in write payloads
        var readOnlyPrimaryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "accountid", "contactid", "leadid", "opportunityid", "incidentid",
            "systemuserid", "teamid", "businessunitid", "organizationid"
        };

        var sanitized = new Dictionary<string, object?>(fields.Count);
        foreach (var kvp in fields)
        {
            if (readOnlyPrimaryKeys.Contains(kvp.Key))
            {
                _logger.LogWarning("Stripping read-only primary key '{FieldName}' from {EntityName} write payload", kvp.Key, entityName);
                continue;
            }
            sanitized[kvp.Key] = kvp.Value;
        }

        return sanitized;
    }

    public override async Task<string> CreateRecordAsync(CrmConnection connection, string entityName, Dictionary<string, object?> fields)
    {
        connection = await EnsureValidTokenAsync(connection);
        fields = SanitizeFieldsForWrite(entityName, fields);

        var collectionName = await GetCollectionNameAsync(connection, entityName);
        var url = $"{GetApiBaseUrl(connection)}/{collectionName}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        AddAuthorizationHeader(request, connection);
        request.Headers.Add("Prefer", "return=representation");

        var json = JsonConvert.SerializeObject(fields);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // Get the ID from the response or OData-EntityId header
        var content = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrEmpty(content))
        {
            var responseObj = JObject.Parse(content);
            var idField = $"{entityName}id";
            return responseObj[idField]?.ToString() ?? "";
        }

        // Try to get from header
        if (response.Headers.TryGetValues("OData-EntityId", out var entityIds))
        {
            var entityIdUrl = entityIds.FirstOrDefault();
            if (entityIdUrl != null)
            {
                // Extract GUID from URL like: https://org.crm.dynamics.com/api/data/v9.2/contacts(guid)
                var match = System.Text.RegularExpressions.Regex.Match(entityIdUrl, @"\(([a-f0-9-]+)\)");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return "";
    }

    public override async Task UpdateRecordAsync(CrmConnection connection, string entityName, string recordId, Dictionary<string, object?> fields)
    {
        connection = await EnsureValidTokenAsync(connection);
        fields = SanitizeFieldsForWrite(entityName, fields);

        var collectionName = await GetCollectionNameAsync(connection, entityName);
        var url = $"{GetApiBaseUrl(connection)}/{collectionName}({recordId})";

        await PatchAsync(connection, url, fields);
    }

    public override async Task DeleteRecordAsync(CrmConnection connection, string entityName, string recordId)
    {
        connection = await EnsureValidTokenAsync(connection);

        var collectionName = await GetCollectionNameAsync(connection, entityName);
        var url = $"{GetApiBaseUrl(connection)}/{collectionName}({recordId})";

        await DeleteAsync(connection, url);
    }

    #endregion

    #region Webhooks

    public override async Task<bool> RegisterWebhookAsync(CrmConnection connection, string webhookUrl, IEnumerable<string> entityNames)
    {
        // Dynamics 365 webhooks require Plugin Registration or Azure Service Bus
        // This is a placeholder - actual implementation requires Dynamics SDK
        // For now, we'll rely on polling for changes

        // TODO: Implement webhook registration via Dynamics Plugin Registration API
        // or provide instructions for manual setup

        await Task.CompletedTask;
        return false;
    }

    public override Task<CrmWebhookEvent?> ParseWebhookPayloadAsync(string payload, IDictionary<string, string> headers)
    {
        try
        {
            var data = JObject.Parse(payload);

            // Parse Dynamics webhook format (from Azure Service Bus or custom plugin)
            var eventType = data["MessageName"]?.ToString()?.ToLower() ?? "update";
            var entityName = data["PrimaryEntityName"]?.ToString() ?? "";
            var recordId = data["PrimaryEntityId"]?.ToString() ?? "";

            var record = new CrmRecord
            {
                EntityName = entityName,
                Id = recordId,
                Fields = new Dictionary<string, object?>()
            };

            // Parse InputParameters/Target for the actual data
            if (data["InputParameters"] is JArray inputParams)
            {
                foreach (var param in inputParams)
                {
                    if (param["key"]?.ToString() == "Target" && param["value"] is JObject target)
                    {
                        foreach (var prop in target.Properties())
                        {
                            record.Fields[prop.Name] = prop.Value.Type == JTokenType.Null ? null : prop.Value;
                        }
                    }
                }
            }

            return Task.FromResult<CrmWebhookEvent?>(new CrmWebhookEvent
            {
                EventType = eventType,
                EntityName = entityName,
                RecordId = recordId,
                Record = record,
                EventTime = DateTimeOffset.UtcNow,
                RawPayload = payload
            });
        }
        catch
        {
            return Task.FromResult<CrmWebhookEvent?>(null);
        }
    }

    public override bool ValidateWebhookSignature(string payload, IDictionary<string, string> headers, string? secret)
    {
        // TODO: Implement signature validation based on Dynamics webhook security model
        // This depends on the specific webhook setup (Azure Service Bus, custom plugin, etc.)
        return true;
    }

    #endregion

    #region Metadata Discovery

    /// <summary>
    /// Gets all lookup fields on an entity that target a specific related entity.
    /// For example, find all lookup fields on 'contact' that point to 'account'.
    /// Also resolves the correct OData navigation property name for polymorphic lookups
    /// (e.g., parentcustomerid → parentcustomerid_account when targeting account).
    /// </summary>
    public override async Task<List<CrmLookupFieldInfo>> GetLookupFieldsAsync(CrmConnection connection, string entityName, string? targetEntityName = null)
    {
        connection = await EnsureValidTokenAsync(connection);

        // Step 1: Get lookup attribute metadata
        var url = $"{GetApiBaseUrl(connection)}/EntityDefinitions(LogicalName='{entityName}')/Attributes/Microsoft.Dynamics.CRM.LookupAttributeMetadata?" +
                  "$select=LogicalName,DisplayName,Targets,AttributeType";

        var response = await GetAsync<JObject>(connection, url);
        var results = new List<CrmLookupFieldInfo>();

        // Step 2: Get many-to-one relationships to resolve navigation property names
        // This is critical for polymorphic lookups (Customer, Owner types) where the
        // @odata.bind must use the navigation property name, not the attribute name
        var relUrl = $"{GetApiBaseUrl(connection)}/EntityDefinitions(LogicalName='{entityName}')/ManyToOneRelationships?" +
                     "$select=ReferencingAttribute,ReferencedEntity,ReferencingEntityNavigationPropertyName";
        var relResponse = await GetAsync<JObject>(connection, relUrl);

        // Build a map: "lookupField:targetEntity" → navigation property name
        var navPropertyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (relResponse?["value"] is JArray relArray)
        {
            foreach (var rel in relArray)
            {
                var refAttribute = rel["ReferencingAttribute"]?.ToString();
                var refEntity = rel["ReferencedEntity"]?.ToString();
                var navProperty = rel["ReferencingEntityNavigationPropertyName"]?.ToString();

                if (!string.IsNullOrEmpty(refAttribute) && !string.IsNullOrEmpty(refEntity) && !string.IsNullOrEmpty(navProperty))
                {
                    navPropertyMap[$"{refAttribute}:{refEntity}"] = navProperty;
                }
            }
        }

        if (response?["value"] is JArray lookupArray)
        {
            foreach (var lookup in lookupArray)
            {
                var logicalName = lookup["LogicalName"]?.ToString();
                var targets = lookup["Targets"] as JArray;
                var targetList = targets?.Select(t => t.ToString()).ToList() ?? new List<string>();
                var attributeType = lookup["AttributeType"]?.ToString();

                if (string.IsNullOrEmpty(logicalName))
                    continue;

                // Filter by target entity if specified
                if (!string.IsNullOrEmpty(targetEntityName) &&
                    !targetList.Any(t => t.Equals(targetEntityName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // For polymorphic lookups (Customer, Owner), resolve the navigation property name
                // The OData API requires the navigation property name, not the attribute logical name
                var isPolymorphic = targetList.Count > 1 ||
                    (attributeType != null && (attributeType == "Customer" || attributeType == "Owner"));

                var bindingFieldName = logicalName;
                if (!string.IsNullOrEmpty(targetEntityName) &&
                    navPropertyMap.TryGetValue($"{logicalName}:{targetEntityName}", out var navProp))
                {
                    bindingFieldName = navProp;
                }

                results.Add(new CrmLookupFieldInfo
                {
                    LogicalName = bindingFieldName,
                    DisplayName = GetLocalizedLabel(lookup["DisplayName"]) ?? logicalName,
                    Targets = targetList
                });
            }
        }

        return results;
    }

    #endregion

    #region Helper Methods

    private string GetApiBaseUrl(CrmConnection connection)
    {
        var baseUrl = connection.EnvironmentUrl?.TrimEnd('/') ?? "https://org.api.crm.dynamics.com";
        return $"{baseUrl}/api/data/{API_VERSION}";
    }

    private string GetResourceUrl(CrmConnection connection)
    {
        // Extract the resource URL from the environment URL
        // e.g., https://org.crm.dynamics.com -> https://org.crm.dynamics.com
        var envUrl = connection.EnvironmentUrl ?? "https://org.crm.dynamics.com";
        return envUrl.Replace(".api.", ".").TrimEnd('/');
    }

    private readonly Dictionary<string, string> _collectionNameCache = new();

    private async Task<string> GetCollectionNameAsync(CrmConnection connection, string entityName)
    {
        if (_collectionNameCache.TryGetValue(entityName, out var cached))
            return cached;

        // Try common pluralization first
        var collectionName = entityName + "s";

        // For entities we know about, use the correct collection name
        var knownCollections = new Dictionary<string, string>
        {
            { "contact", "contacts" },
            { "account", "accounts" },
            { "lead", "leads" },
            { "opportunity", "opportunities" },
            { "systemuser", "systemusers" },
            { "team", "teams" }
        };

        if (knownCollections.TryGetValue(entityName.ToLower(), out var known))
        {
            collectionName = known;
        }
        else
        {
            // Query metadata for the actual collection name
            try
            {
                var url = $"{GetApiBaseUrl(connection)}/EntityDefinitions(LogicalName='{entityName}')?$select=EntitySetName";
                var response = await GetAsync<JObject>(connection, url);
                collectionName = response?["EntitySetName"]?.ToString() ?? collectionName;
            }
            catch
            {
                // Fall back to simple pluralization
            }
        }

        _collectionNameCache[entityName] = collectionName;
        return collectionName;
    }

    private CrmRecord ParseCrmRecord(string entityName, JObject data)
    {
        var record = new CrmRecord
        {
            EntityName = entityName,
            Id = data[$"{entityName}id"]?.ToString() ?? data["id"]?.ToString() ?? "",
            Fields = new Dictionary<string, object?>()
        };

        foreach (var prop in data.Properties())
        {
            // Skip OData annotations (like @odata.context, @odata.etag)
            // But KEEP lookup value fields that start with _ and end with _value
            // (e.g., _bcbi_companyid_value, _parentcustomerid_value)
            if (prop.Name.StartsWith("@"))
                continue;

            // Skip internal underscore fields EXCEPT lookup values (ending with _value)
            if (prop.Name.StartsWith("_") && !prop.Name.EndsWith("_value"))
                continue;

            record.Fields[prop.Name] = ConvertJTokenValue(prop.Value);
        }

        // Parse dates
        if (data["createdon"] != null)
            record.CreatedOn = ParseDateTimeOffset(data["createdon"]);
        if (data["modifiedon"] != null)
            record.ModifiedOn = ParseDateTimeOffset(data["modifiedon"]);

        return record;
    }

    /// <summary>
    /// Converts a JToken value to a .NET object, ensuring DateTime values are converted to DateTimeOffset
    /// </summary>
    private object? ConvertJTokenValue(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;

        switch (token.Type)
        {
            case JTokenType.Date:
                // Convert DateTime to DateTimeOffset to avoid casting issues
                var dateValue = token.Value<DateTime>();
                return new DateTimeOffset(dateValue, TimeSpan.Zero);

            case JTokenType.String:
                // Check if string looks like a date and parse it
                var strValue = token.ToString();
                if (DateTime.TryParse(strValue, out var parsedDate))
                {
                    // Only convert if it looks like an ISO date string
                    if (strValue.Contains("T") || strValue.Contains("-"))
                    {
                        if (DateTimeOffset.TryParse(strValue, out var dto))
                            return dto;
                        return new DateTimeOffset(parsedDate, TimeSpan.Zero);
                    }
                }
                return strValue;

            case JTokenType.Integer:
                return token.Value<long>();

            case JTokenType.Float:
                return token.Value<decimal>();

            case JTokenType.Boolean:
                return token.Value<bool>();

            case JTokenType.Guid:
                return token.Value<Guid>();

            default:
                return token.ToObject<object>();
        }
    }

    /// <summary>
    /// Safely parses a JToken to DateTimeOffset
    /// </summary>
    private DateTimeOffset? ParseDateTimeOffset(JToken? token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;

        try
        {
            if (token.Type == JTokenType.Date)
            {
                var dt = token.Value<DateTime>();
                return new DateTimeOffset(dt, TimeSpan.Zero);
            }

            if (DateTimeOffset.TryParse(token.ToString(), out var result))
                return result;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string MapDynamicsAttributeType(string dynamicsType)
    {
        return dynamicsType switch
        {
            "String" => "String",
            "Memo" => "String",
            "Integer" => "Integer",
            "BigInt" => "Integer",
            "Double" => "Decimal",
            "Decimal" => "Decimal",
            "Money" => "Decimal",
            "Boolean" => "Boolean",
            "DateTime" => "DateTime",
            "Lookup" => "Lookup",
            "Customer" => "Lookup",
            "Owner" => "Lookup",
            "Picklist" => "OptionSet",
            "State" => "OptionSet",
            "Status" => "OptionSet",
            "Uniqueidentifier" => "Guid",
            _ => "String"
        };
    }

    #endregion
}
