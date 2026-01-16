using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AuthScape.ErrorTracking.Hubs;
using AuthScape.Models;
using AuthScape.Models.ErrorTracking;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.Context;
using UAParser;

namespace AuthScape.ErrorTracking.Services;

/// <summary>
/// Core service for logging, tracking, and managing errors across API, IDP, and Frontend.
/// Provides methods for error logging, grouping, querying, and resolution tracking.
/// </summary>
public class ErrorTrackingService : IErrorTrackingService
{
    private readonly DatabaseContext _context;
    private readonly IErrorGroupingService _groupingService;
    private readonly ILogger<ErrorTrackingService> _logger;
    private readonly IHubContext<ErrorTrackingHub>? _hubContext;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IConfiguration? _configuration;

    public ErrorTrackingService(
        DatabaseContext context,
        IErrorGroupingService groupingService,
        ILogger<ErrorTrackingService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IHubContext<ErrorTrackingHub> hubContext)
    {
        _context = context;
        _groupingService = groupingService;
        _logger = logger;
        _hubContext = hubContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;

        _logger.LogInformation("ErrorTrackingService initialized. HubContext={HasHub}, HttpClientFactory={HasHttp}, Configuration={HasConfig}",
            _hubContext != null, _httpClientFactory != null, _configuration != null);
    }

    /// <summary>
    /// Logs an error from the backend (API or IDP) with full HTTP context.
    /// </summary>
    public async Task<Guid> LogError(
        Exception exception,
        HttpContext httpContext,
        ErrorSource source,
        long? responseTimeMs = null,
        Guid? sessionId = null)
    {
        try
        {
            _logger.LogInformation("LogError called for {ExceptionType}", exception.GetType().Name);

            // Get settings to determine if we should track this error
            var settings = await GetOrCreateSettings();
            _logger.LogInformation("Settings retrieved. Track500Errors={Track500}", settings.Track500Errors);

            var statusCode = httpContext.Response.StatusCode;
            _logger.LogInformation("Status code: {StatusCode}", statusCode);

            if (!ShouldTrackStatusCode(statusCode, settings))
            {
                _logger.LogWarning("Status code {StatusCode} is not configured to be tracked", statusCode);
                return Guid.Empty;
            }

            _logger.LogInformation("Status code {StatusCode} will be tracked", statusCode);

            // Parse user agent
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
            var uaParser = Parser.GetDefault();
            var clientInfo = uaParser.Parse(userAgent);

            // Capture request body (if configured)
            string requestBody = null;
            bool requestBodyTruncated = false;

            if (settings.CaptureRequestBody && httpContext.Request.ContentLength.HasValue)
            {
                httpContext.Request.EnableBuffering();
                httpContext.Request.Body.Position = 0;
                using var reader = new System.IO.StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                httpContext.Request.Body.Position = 0;

                if (settings.AutoRedactSensitiveData)
                    body = RedactSensitiveData(body, settings.CustomRedactionFields);

                if (body.Length > settings.MaxBodySizeKB * 1024)
                {
                    requestBody = body.Substring(0, settings.MaxBodySizeKB * 1024);
                    requestBodyTruncated = true;
                }
                else
                {
                    requestBody = body;
                }
            }

            // Capture performance metrics
            long? memoryUsageMB = null;
            int? threadCount = null;

            if (settings.CapturePerformanceMetrics)
            {
                var process = Process.GetCurrentProcess();
                memoryUsageMB = process.WorkingSet64 / 1024 / 1024;
                threadCount = process.Threads.Count;
            }

            // Get user ID from claims
            long? userId = null;
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = httpContext.User.FindFirst("sub")?.Value ??
                                  httpContext.User.FindFirst("userId")?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out var parsedUserId))
                    userId = parsedUserId;
            }

            // Get analytics session ID if configured
            Guid? analyticsSessionId = null;
            if (settings.LinkToAnalyticsSessions && sessionId.HasValue)
            {
                analyticsSessionId = sessionId;
            }

            // Generate error signature for grouping
            var errorType = exception.GetType().FullName ?? "UnknownError";
            var endpoint = $"{httpContext.Request.Method} {httpContext.Request.Path}";
            var stackTrace = exception.StackTrace ?? string.Empty;

            var errorSignature = _groupingService.GenerateErrorSignature(errorType, stackTrace, endpoint);

            // Find or create error group
            var errorGroup = await _context.ErrorGroups.FirstOrDefaultAsync(g => g.ErrorSignature == errorSignature);

            if (errorGroup == null)
            {
                errorGroup = new ErrorGroup
                {
                    Id = Guid.NewGuid(),
                    ErrorSignature = errorSignature,
                    ErrorMessage = _groupingService.GetConciseErrorMessage(exception.Message),
                    ErrorType = _groupingService.GetSimpleErrorType(errorType),
                    Endpoint = endpoint,
                    StatusCode = statusCode,
                    Source = source,
                    OccurrenceCount = 1,
                    FirstSeen = DateTimeOffset.UtcNow,
                    LastSeen = DateTimeOffset.UtcNow,
                    IsResolved = false,
                    SampleStackTrace = stackTrace,
                    Environment = GetEnvironmentFromConfig(),
                    ResolutionNotes = string.Empty // Required - cannot be NULL
                };

                _context.ErrorGroups.Add(errorGroup);
            }
            else
            {
                errorGroup.OccurrenceCount++;
                errorGroup.LastSeen = DateTimeOffset.UtcNow;
            }

            // Create error log entry - all string fields must have values (NOT NULL constraints)
            var errorLog = new ErrorLog
            {
                Id = Guid.NewGuid(),
                ErrorGroupId = errorGroup.Id,
                ErrorMessage = exception.Message ?? string.Empty,
                ErrorType = errorType ?? string.Empty,
                StackTrace = stackTrace ?? string.Empty,
                StatusCode = statusCode,
                Endpoint = endpoint ?? string.Empty,
                HttpMethod = httpContext.Request.Method ?? string.Empty,
                RequestBody = requestBody ?? string.Empty,
                RequestBodyTruncated = requestBodyTruncated,
                ResponseBody = string.Empty,
                ResponseBodyTruncated = false,
                QueryString = httpContext.Request.QueryString.ToString() ?? string.Empty,
                RequestHeaders = settings.CaptureHeaders ? (SerializeHeaders(httpContext.Request.Headers) ?? string.Empty) : string.Empty,
                ResponseHeaders = string.Empty,
                UserId = userId,
                Username = httpContext.User?.Identity?.Name ?? string.Empty,
                UserEmail = string.Empty,
                IPAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                UserAgent = userAgent ?? string.Empty,
                Browser = clientInfo.UA.Family ?? string.Empty,
                BrowserVersion = clientInfo.UA.Major ?? string.Empty,
                OperatingSystem = clientInfo.OS.Family ?? string.Empty,
                DeviceType = clientInfo.Device.Family ?? string.Empty,
                AnalyticsSessionId = analyticsSessionId,
                Environment = GetEnvironmentFromConfig(),
                DatabaseProvider = string.Empty,
                MachineName = Environment.MachineName ?? string.Empty,
                ApplicationVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty,
                ResponseTimeMs = responseTimeMs,
                MemoryUsageMB = memoryUsageMB,
                ThreadCount = threadCount,
                Source = source,
                ComponentName = string.Empty,
                IsResolved = false,
                Created = DateTimeOffset.UtcNow,
                AdditionalMetadata = string.Empty,
                ResolutionNotes = string.Empty
            };

            _logger.LogInformation("Adding ErrorLog to context with ID {ErrorLogId}", errorLog.Id);
            _context.ErrorLogs.Add(errorLog);

            _logger.LogInformation("Calling SaveChangesAsync...");
            var saved = await _context.SaveChangesAsync();
            _logger.LogInformation("SaveChangesAsync completed. Rows affected: {RowsAffected}", saved);

            _logger.LogInformation("Error logged successfully: {ErrorLogId} - {Message}", errorLog.Id, exception.Message);

            // Send real-time notification via SignalR
            await NotifyNewError(errorLog);

            return errorLog.Id;
        }
        catch (Exception ex)
        {
            // Don't let error tracking crash the applicationB
            _logger.LogError(ex, "Failed to log error to database: {ExceptionMessage}", ex.Message);
            return Guid.Empty;
        }
    }

    /// <summary>
    /// Logs a single error from the frontend.
    /// </summary>
    public async Task<Guid> LogFrontendError(FrontendErrorDto error)
    {
        try
        {
            var settings = await GetOrCreateSettings();

            if (!settings.EnableFrontendTracking)
                return Guid.Empty;

            // Generate error signature
            var errorSignature = _groupingService.GenerateErrorSignature(
                error.ErrorType ?? "JavaScriptError",
                error.StackTrace ?? string.Empty,
                error.Url ?? string.Empty);

            // Find or create error group
            var errorGroup = await _context.ErrorGroups.FirstOrDefaultAsync(g => g.ErrorSignature == errorSignature);

            if (errorGroup == null)
            {
                errorGroup = new ErrorGroup
                {
                    Id = Guid.NewGuid(),
                    ErrorSignature = errorSignature,
                    ErrorMessage = _groupingService.GetConciseErrorMessage(error.Message),
                    ErrorType = _groupingService.GetSimpleErrorType(error.ErrorType ?? "JavaScriptError"),
                    Endpoint = error.Url ?? string.Empty,
                    StatusCode = 0, // Frontend errors don't have HTTP status codes
                    Source = ErrorSource.Frontend,
                    OccurrenceCount = 1,
                    FirstSeen = DateTimeOffset.UtcNow,
                    LastSeen = DateTimeOffset.UtcNow,
                    IsResolved = false,
                    SampleStackTrace = error.StackTrace ?? string.Empty,
                    Environment = GetEnvironmentFromConfig(),
                    ResolutionNotes = string.Empty // Required - cannot be NULL
                };

                _context.ErrorGroups.Add(errorGroup);
            }
            else
            {
                errorGroup.OccurrenceCount++;
                errorGroup.LastSeen = DateTimeOffset.UtcNow;
            }

            // Parse user agent if provided
            string browser = null, browserVersion = null, os = null, deviceType = null;

            if (!string.IsNullOrEmpty(error.UserAgent))
            {
                var uaParser = Parser.GetDefault();
                var clientInfo = uaParser.Parse(error.UserAgent);
                browser = clientInfo.UA.Family;
                browserVersion = clientInfo.UA.Major;
                os = clientInfo.OS.Family;
                deviceType = clientInfo.Device.Family;
            }

            // Look up username and email from UserId if provided
            string username = string.Empty;
            string userEmail = string.Empty;
            if (error.UserId.HasValue)
            {
                var user = await _context.Users.FindAsync(error.UserId.Value);
                if (user != null)
                {
                    username = $"{user.FirstName} {user.LastName}".Trim();
                    userEmail = user.Email ?? string.Empty;
                }
            }

            // Create error log entry - all string fields must have values (NOT NULL constraints)
            var errorLog = new ErrorLog
            {
                Id = Guid.NewGuid(),
                ErrorGroupId = errorGroup.Id,
                ErrorMessage = error.Message ?? string.Empty,
                ErrorType = error.ErrorType ?? "JavaScriptError",
                StackTrace = error.StackTrace ?? string.Empty,
                StatusCode = 0,
                Endpoint = error.Url ?? string.Empty,
                HttpMethod = "GET", // Frontend errors typically from page loads
                RequestBody = string.Empty,
                RequestBodyTruncated = false,
                ResponseBody = string.Empty,
                ResponseBodyTruncated = false,
                QueryString = string.Empty,
                RequestHeaders = string.Empty,
                ResponseHeaders = string.Empty,
                UserId = error.UserId,
                Username = username,
                UserEmail = userEmail,
                IPAddress = error.IPAddress ?? string.Empty,
                UserAgent = error.UserAgent ?? string.Empty,
                Browser = browser ?? string.Empty,
                BrowserVersion = browserVersion ?? string.Empty,
                OperatingSystem = os ?? string.Empty,
                DeviceType = deviceType ?? string.Empty,
                AnalyticsSessionId = error.SessionId,
                Environment = GetEnvironmentFromConfig(),
                DatabaseProvider = string.Empty,
                MachineName = string.Empty,
                ApplicationVersion = string.Empty,
                ComponentName = error.ComponentName ?? string.Empty,
                Source = ErrorSource.Frontend,
                IsResolved = false,
                AdditionalMetadata = error.Metadata ?? string.Empty,
                ResolutionNotes = string.Empty,
                Created = DateTimeOffset.UtcNow
            };

            _context.ErrorLogs.Add(errorLog);
            await _context.SaveChangesAsync();

            // Send real-time notification via SignalR
            await NotifyNewError(errorLog);

            return errorLog.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log frontend error to database");
            return Guid.Empty;
        }
    }

    /// <summary>
    /// Logs a batch of frontend errors (for batched reporting).
    /// </summary>
    public async Task<List<Guid>> LogBatchErrors(List<FrontendErrorDto> errors)
    {
        var errorIds = new List<Guid>();

        foreach (var error in errors)
        {
            var errorId = await LogFrontendError(error);
            if (errorId != Guid.Empty)
                errorIds.Add(errorId);
        }

        return errorIds;
    }

    /// <summary>
    /// Queries error groups with filtering and pagination.
    /// </summary>
    public async Task<ErrorGroupQueryResult> GetErrorGroups(ErrorGroupQueryDto query)
    {
        var queryable = _context.ErrorGroups.AsQueryable();

        // Apply filters
        if (query.Source.HasValue)
            queryable = queryable.Where(g => g.Source == query.Source.Value);

        if (query.Environment.HasValue)
            queryable = queryable.Where(g => g.Environment == query.Environment.Value);

        if (query.StatusCode.HasValue)
            queryable = queryable.Where(g => g.StatusCode == query.StatusCode.Value);

        if (query.IsResolved.HasValue)
            queryable = queryable.Where(g => g.IsResolved == query.IsResolved.Value);

        if (query.StartDate.HasValue)
            queryable = queryable.Where(g => g.LastSeen >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            queryable = queryable.Where(g => g.LastSeen <= query.EndDate.Value);

        if (!string.IsNullOrEmpty(query.SearchTerm))
            queryable = queryable.Where(g =>
                g.ErrorMessage.Contains(query.SearchTerm) ||
                g.ErrorType.Contains(query.SearchTerm) ||
                g.Endpoint.Contains(query.SearchTerm));

        // Get total count before pagination
        var totalCount = await queryable.CountAsync();

        // Apply sorting
        queryable = query.SortBy?.ToLower() switch
        {
            "occurrences" => query.SortDescending
                ? queryable.OrderByDescending(g => g.OccurrenceCount)
                : queryable.OrderBy(g => g.OccurrenceCount),
            "firstseen" => query.SortDescending
                ? queryable.OrderByDescending(g => g.FirstSeen)
                : queryable.OrderBy(g => g.FirstSeen),
            _ => queryable.OrderByDescending(g => g.LastSeen) // Default: most recent first
        };

        // Apply pagination
        var errorGroups = await queryable
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new ErrorGroupQueryResult
        {
            ErrorGroups = errorGroups,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
        };
    }

    /// <summary>
    /// Gets individual error occurrences for a specific error group.
    /// </summary>
    public async Task<List<ErrorLog>> GetErrorOccurrences(Guid errorGroupId, int page = 1, int pageSize = 50)
    {
        return await _context.ErrorLogs
            .Where(e => e.ErrorGroupId == errorGroupId)
            .OrderByDescending(e => e.Created)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>
    /// Marks an error group as resolved.
    /// </summary>
    public async Task<bool> MarkErrorGroupResolved(Guid errorGroupId, long resolvedByUserId, string resolutionNotes)
    {
        var errorGroup = await _context.ErrorGroups.FindAsync(errorGroupId);

        if (errorGroup == null)
            return false;

        errorGroup.IsResolved = true;
        errorGroup.ResolvedAt = DateTimeOffset.UtcNow;
        errorGroup.ResolvedByUserId = resolvedByUserId;
        errorGroup.ResolutionNotes = resolutionNotes;

        // Also mark all error logs in this group as resolved
        var errorLogs = await _context.ErrorLogs
            .Where(e => e.ErrorGroupId == errorGroupId && !e.IsResolved)
            .ToListAsync();

        foreach (var log in errorLogs)
        {
            log.IsResolved = true;
            log.ResolvedAt = DateTimeOffset.UtcNow;
            log.ResolvedByUserId = resolvedByUserId;
            log.ResolutionNotes = resolutionNotes;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Gets or creates the error tracking settings singleton.
    /// </summary>
    public async Task<ErrorTrackingSettings> GetOrCreateSettings()
    {
        var settings = await _context.ErrorTrackingSettings.FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = new ErrorTrackingSettings
            {
                Track400Errors = true,
                Track401Errors = true,
                Track403Errors = true,
                Track404Errors = true,
                Track500Errors = true,
                Track502Errors = true,
                Track503Errors = true,
                EnableFrontendTracking = true,
                FrontendBatchIntervalSeconds = 30,
                RetentionPeriodDays = 90,
                MaxBodySizeKB = 50,
                CaptureRequestBody = true,
                CaptureResponseBody = false,
                CaptureHeaders = true,
                CapturePerformanceMetrics = true,
                LinkToAnalyticsSessions = true,
                AutoRedactSensitiveData = true,
                CustomStatusCodes = string.Empty,
                CustomRedactionFields = string.Empty,
                NotifyForStatusCodes = string.Empty,
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow
            };

            _context.ErrorTrackingSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }

    /// <summary>
    /// Updates error tracking settings.
    /// </summary>
    public async Task<bool> UpdateSettings(ErrorTrackingSettings settings)
    {
        var existing = await _context.ErrorTrackingSettings.FindAsync(settings.Id);

        if (existing == null)
            return false;

        // Update properties
        existing.Track400Errors = settings.Track400Errors;
        existing.Track401Errors = settings.Track401Errors;
        existing.Track403Errors = settings.Track403Errors;
        existing.Track404Errors = settings.Track404Errors;
        existing.Track500Errors = settings.Track500Errors;
        existing.Track502Errors = settings.Track502Errors;
        existing.Track503Errors = settings.Track503Errors;
        existing.CustomStatusCodes = settings.CustomStatusCodes;
        existing.EnableFrontendTracking = settings.EnableFrontendTracking;
        existing.FrontendBatchIntervalSeconds = settings.FrontendBatchIntervalSeconds;
        existing.RetentionPeriodDays = settings.RetentionPeriodDays;
        existing.MaxBodySizeKB = settings.MaxBodySizeKB;
        existing.CaptureRequestBody = settings.CaptureRequestBody;
        existing.CaptureResponseBody = settings.CaptureResponseBody;
        existing.CaptureHeaders = settings.CaptureHeaders;
        existing.CapturePerformanceMetrics = settings.CapturePerformanceMetrics;
        existing.LinkToAnalyticsSessions = settings.LinkToAnalyticsSessions;
        existing.AutoRedactSensitiveData = settings.AutoRedactSensitiveData;
        existing.CustomRedactionFields = settings.CustomRedactionFields;
        existing.EnableErrorNotifications = settings.EnableErrorNotifications;
        existing.NotifyForStatusCodes = settings.NotifyForStatusCodes;
        existing.Modified = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    // Helper methods

    private bool ShouldTrackStatusCode(int statusCode, ErrorTrackingSettings settings)
    {
        // Check standard tracked status codes
        if (statusCode == 400 && settings.Track400Errors) return true;
        if (statusCode == 401 && settings.Track401Errors) return true;
        if (statusCode == 403 && settings.Track403Errors) return true;
        if (statusCode == 404 && settings.Track404Errors) return true;
        if (statusCode == 500 && settings.Track500Errors) return true;
        if (statusCode == 502 && settings.Track502Errors) return true;
        if (statusCode == 503 && settings.Track503Errors) return true;

        // Check custom status codes
        if (!string.IsNullOrEmpty(settings.CustomStatusCodes))
        {
            var customCodes = settings.CustomStatusCodes.Split(',')
                .Select(c => c.Trim())
                .Where(c => int.TryParse(c, out _))
                .Select(int.Parse);

            if (customCodes.Contains(statusCode))
                return true;
        }

        return false;
    }

    private string RedactSensitiveData(string data, string customFields)
    {
        if (string.IsNullOrEmpty(data))
            return data;

        // Standard sensitive fields
        var sensitiveFields = new List<string>
        {
            "password", "Password", "PASSWORD",
            "token", "Token", "TOKEN",
            "apiKey", "api_key", "ApiKey", "API_KEY",
            "secret", "Secret", "SECRET",
            "creditCard", "credit_card", "CreditCard",
            "ssn", "SSN", "socialSecurityNumber"
        };

        // Add custom fields if provided
        if (!string.IsNullOrEmpty(customFields))
        {
            var customFieldsList = customFields.Split(',').Select(f => f.Trim());
            sensitiveFields.AddRange(customFieldsList);
        }

        var redactedData = data;

        foreach (var field in sensitiveFields)
        {
            // Redact JSON properties: "field": "value" -> "field": "[REDACTED]"
            redactedData = Regex.Replace(redactedData,
                $@"""{field}""\s*:\s*""[^""]*""",
                $@"""{field}"": ""[REDACTED]""",
                RegexOptions.IgnoreCase);
        }

        return redactedData;
    }

    private string SerializeHeaders(IHeaderDictionary headers)
    {
        try
        {
            var headerDict = headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            return JsonSerializer.Serialize(headerDict);
        }
        catch
        {
            return null;
        }
    }

    private Stage GetEnvironmentFromConfig()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        return environmentName.ToLower() switch
        {
            "development" => Stage.Development,
            "staging" => Stage.Staging,
            "production" => Stage.Production,
            _ => Stage.Development
        };
    }

    /// <summary>
    /// Sends a real-time notification to all connected clients when a new error is logged.
    /// Uses SignalR directly if hub context is available (IDP), otherwise calls IDP API endpoint (API).
    /// </summary>
    private async Task NotifyNewError(ErrorLog errorLog)
    {
        var notification = new
        {
            errorLog.Id,
            errorLog.ErrorGroupId,
            errorLog.ErrorMessage,
            errorLog.ErrorType,
            errorLog.StatusCode,
            errorLog.Endpoint,
            errorLog.HttpMethod,
            errorLog.Browser,
            errorLog.OperatingSystem,
            Source = errorLog.Source.ToString(),
            errorLog.IsResolved,
            errorLog.Created
        };

        // Check if we should use HTTP to call IDP (when IDPUrl is configured, we're on the API)
        var idpUrl = _configuration?.GetValue<string>("AppSettings:IDPUrl");
        var useHttpToIdp = !string.IsNullOrEmpty(idpUrl) && _httpClientFactory != null;

        _logger.LogInformation("NotifyNewError: Starting notification for error {ErrorId}. HubContext={HasHub}, UseHttpToIdp={UseHttp}, IdpUrl={IdpUrl}",
            errorLog.Id, _hubContext != null, useHttpToIdp, idpUrl ?? "not configured");

        // If IDPUrl is configured, we're on the API - call IDP via HTTP
        if (useHttpToIdp)
        {
            try
            {
                _logger.LogInformation("NotifyNewError: Sending notification via HTTP to IDP at {IdpUrl}", idpUrl);

                var client = _httpClientFactory!.CreateClient();
                var json = JsonSerializer.Serialize(notification);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{idpUrl}/api/ErrorTrackingHub/NotifyNewError", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("SignalR notification sent via IDP for error {ErrorId}", errorLog.Id);
                }
                else
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to send SignalR notification via IDP: {StatusCode} - {ResponseBody}", response.StatusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR notification via IDP for error {ErrorId}", errorLog.Id);
            }
        }
        // No IDPUrl configured means we're on the IDP - use SignalR directly
        else if (_hubContext != null)
        {
            try
            {
                _logger.LogInformation("NotifyNewError: Sending SignalR notification directly to 'error_tracking' group");
                await _hubContext.Clients.Group("error_tracking").SendAsync("NewError", notification);

                if (errorLog.ErrorGroupId.HasValue)
                {
                    await _hubContext.Clients.Group($"error_group_{errorLog.ErrorGroupId}").SendAsync("NewOccurrence", notification);
                }

                _logger.LogInformation("SignalR notification sent directly for error {ErrorId}", errorLog.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR notification for error {ErrorId}", errorLog.Id);
            }
        }
        else
        {
            _logger.LogWarning("NotifyNewError: No SignalR hub context or IDP URL configured - real-time notifications disabled");
        }
    }
}
