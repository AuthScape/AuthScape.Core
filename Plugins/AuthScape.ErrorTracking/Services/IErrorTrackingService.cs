using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuthScape.Models;
using AuthScape.Models.ErrorTracking;
using Microsoft.AspNetCore.Http;

namespace AuthScape.ErrorTracking.Services;

public interface IErrorTrackingService
{
    Task<Guid> LogError(Exception exception, HttpContext httpContext, ErrorSource source, long? responseTimeMs = null, Guid? sessionId = null);
    Task<Guid> LogFrontendError(FrontendErrorDto error);
    Task<List<Guid>> LogBatchErrors(List<FrontendErrorDto> errors);
    Task<ErrorGroupQueryResult> GetErrorGroups(ErrorGroupQueryDto query);
    Task<List<ErrorLog>> GetErrorOccurrences(Guid errorGroupId, int page = 1, int pageSize = 50);
    Task<bool> MarkErrorGroupResolved(Guid errorGroupId, long resolvedByUserId, string resolutionNotes);
    Task<ErrorTrackingSettings> GetOrCreateSettings();
    Task<bool> UpdateSettings(ErrorTrackingSettings settings);
}

/// <summary>
/// DTO for logging errors from the frontend (React app).
/// </summary>
public class FrontendErrorDto
{
    public string Message { get; set; }
    public string ErrorType { get; set; }
    public string StackTrace { get; set; }
    public string Url { get; set; }
    public string ComponentName { get; set; }
    public long? UserId { get; set; }
    public string IPAddress { get; set; }
    public string UserAgent { get; set; }
    public Guid? SessionId { get; set; }
    public string Metadata { get; set; }
}

/// <summary>
/// Query parameters for filtering error groups.
/// </summary>
public class ErrorGroupQueryDto
{
    public ErrorSource? Source { get; set; }
    public Stage? Environment { get; set; }
    public int? StatusCode { get; set; }
    public bool? IsResolved { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public string SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "LastSeen";
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Result of error group query with pagination info.
/// </summary>
public class ErrorGroupQueryResult
{
    public List<ErrorGroup> ErrorGroups { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
