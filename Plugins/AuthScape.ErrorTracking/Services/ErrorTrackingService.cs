using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AuthScape.Models;
using AuthScape.Models.ErrorTracking;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

    public ErrorTrackingService(
        DatabaseContext context,
        IErrorGroupingService groupingService,
        ILogger<ErrorTrackingService> logger)
    {
        _context = context;
        _groupingService = groupingService;
        _logger = logger;
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
            // Get settings to determine if we should track this error
            var settings = await GetOrCreateSettings();

            var statusCode = httpContext.Response.StatusCode;

            if (!ShouldTrackStatusCode(statusCode, settings))
                return Guid.Empty;

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
                    Environment = GetEnvironmentFromConfig()
                };

                _context.ErrorGroups.Add(errorGroup);
            }
            else
            {
                errorGroup.OccurrenceCount++;
                errorGroup.LastSeen = DateTimeOffset.UtcNow;
            }

            // Create error log entry
            var errorLog = new ErrorLog
            {
                Id = Guid.NewGuid(),
                ErrorGroupId = errorGroup.Id,
                ErrorMessage = exception.Message,
                ErrorType = errorType,
                StackTrace = stackTrace,
                StatusCode = statusCode,
                Endpoint = endpoint,
                HttpMethod = httpContext.Request.Method,
                RequestBody = requestBody,
                RequestBodyTruncated = requestBodyTruncated,
                QueryString = httpContext.Request.QueryString.ToString(),
                RequestHeaders = settings.CaptureHeaders ? SerializeHeaders(httpContext.Request.Headers) : null,
                UserId = userId,
                IPAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = userAgent,
                Browser = clientInfo.UA.Family,
                BrowserVersion = clientInfo.UA.Major,
                OperatingSystem = clientInfo.OS.Family,
                DeviceType = clientInfo.Device.Family,
                AnalyticsSessionId = analyticsSessionId,
                Environment = GetEnvironmentFromConfig(),
                MachineName = Environment.MachineName,
                ResponseTimeMs = responseTimeMs,
                MemoryUsageMB = memoryUsageMB,
                ThreadCount = threadCount,
                Source = source,
                IsResolved = false,
                Created = DateTimeOffset.UtcNow
            };

            _context.ErrorLogs.Add(errorLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Error logged: {errorLog.Id} - {exception.Message}");

            return errorLog.Id;
        }
        catch (Exception ex)
        {
            // Don't let error tracking crash the application
            _logger.LogError(ex, "Failed to log error to database");
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
                    Endpoint = error.Url,
                    StatusCode = 0, // Frontend errors don't have HTTP status codes
                    Source = ErrorSource.Frontend,
                    OccurrenceCount = 1,
                    FirstSeen = DateTimeOffset.UtcNow,
                    LastSeen = DateTimeOffset.UtcNow,
                    IsResolved = false,
                    SampleStackTrace = error.StackTrace,
                    Environment = GetEnvironmentFromConfig()
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

            // Create error log entry
            var errorLog = new ErrorLog
            {
                Id = Guid.NewGuid(),
                ErrorGroupId = errorGroup.Id,
                ErrorMessage = error.Message,
                ErrorType = error.ErrorType ?? "JavaScriptError",
                StackTrace = error.StackTrace,
                StatusCode = 0,
                Endpoint = error.Url,
                HttpMethod = "GET", // Frontend errors typically from page loads
                UserId = error.UserId,
                IPAddress = error.IPAddress,
                UserAgent = error.UserAgent,
                Browser = browser,
                BrowserVersion = browserVersion,
                OperatingSystem = os,
                DeviceType = deviceType,
                AnalyticsSessionId = error.SessionId,
                Environment = GetEnvironmentFromConfig(),
                ComponentName = error.ComponentName,
                Source = ErrorSource.Frontend,
                IsResolved = false,
                AdditionalMetadata = error.Metadata,
                Created = DateTimeOffset.UtcNow
            };

            _context.ErrorLogs.Add(errorLog);
            await _context.SaveChangesAsync();

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
                Track500Errors = true,
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
}
