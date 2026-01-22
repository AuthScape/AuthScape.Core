using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuthScape.IDP.Services.ErrorTracking;
using AuthScape.Models.ErrorTracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AuthScape.IDP.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ErrorTrackingController : ControllerBase
{
    private readonly IErrorTrackingService _errorTrackingService;
    private readonly ILogger<ErrorTrackingController> _logger;

    public ErrorTrackingController(
        IErrorTrackingService errorTrackingService,
        ILogger<ErrorTrackingController> logger)
    {
        _errorTrackingService = errorTrackingService;
        _logger = logger;
    }

    /// <summary>
    /// Logs a single error from the frontend.
    /// </summary>
    [HttpPost("LogError")]
    [AllowAnonymous] // Allow unauthenticated users to log errors
    public async Task<IActionResult> LogError([FromBody] FrontendErrorDto error)
    {
        try
        {
            // Capture client IP if not provided
            if (string.IsNullOrEmpty(error.IPAddress))
            {
                error.IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            // Capture user agent if not provided
            if (string.IsNullOrEmpty(error.UserAgent))
            {
                error.UserAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            }

            var errorId = await _errorTrackingService.LogFrontendError(error);

            return Ok(new { errorId, success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log frontend error");
            return StatusCode(500, new { success = false, error = "Failed to log error" });
        }
    }

    /// <summary>
    /// Logs a batch of errors from the frontend (for batched reporting).
    /// </summary>
    [HttpPost("LogBatch")]
    [AllowAnonymous]
    public async Task<IActionResult> LogBatch([FromBody] List<FrontendErrorDto> errors)
    {
        try
        {
            // Enrich errors with IP and user agent if not provided
            foreach (var error in errors)
            {
                if (string.IsNullOrEmpty(error.IPAddress))
                    error.IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                if (string.IsNullOrEmpty(error.UserAgent))
                    error.UserAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            }

            var errorIds = await _errorTrackingService.LogBatchErrors(errors);

            return Ok(new { errorIds, count = errorIds.Count, success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log batch errors");
            return StatusCode(500, new { success = false, error = "Failed to log errors" });
        }
    }

    /// <summary>
    /// Gets error groups with filtering and pagination.
    /// </summary>
    [HttpPost("GetErrors")]
    [Authorize] // Require authentication for viewing errors
    public async Task<IActionResult> GetErrors([FromBody] ErrorGroupQueryDto query)
    {
        try
        {
            var result = await _errorTrackingService.GetErrorGroups(query);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get error groups");
            return StatusCode(500, new { success = false, error = "Failed to retrieve errors" });
        }
    }

    /// <summary>
    /// Gets individual error occurrences for a specific error group.
    /// </summary>
    [HttpGet("GetErrorGroup")]
    [Authorize]
    public async Task<IActionResult> GetErrorGroup([FromQuery] Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var occurrences = await _errorTrackingService.GetErrorOccurrences(id, page, pageSize);

            return Ok(new { errorGroupId = id, occurrences, page, pageSize });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get error occurrences");
            return StatusCode(500, new { success = false, error = "Failed to retrieve error occurrences" });
        }
    }

    /// <summary>
    /// Marks an error group as resolved.
    /// </summary>
    [HttpPut("MarkResolved")]
    [Authorize]
    public async Task<IActionResult> MarkResolved([FromBody] ResolveErrorRequest request)
    {
        try
        {
            // Get user ID from claims
            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var success = await _errorTrackingService.MarkErrorGroupResolved(
                request.ErrorGroupId,
                userId,
                request.ResolutionNotes);

            if (!success)
                return NotFound(new { success = false, error = "Error group not found" });

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark error as resolved");
            return StatusCode(500, new { success = false, error = "Failed to mark error as resolved" });
        }
    }

    /// <summary>
    /// Gets the current error tracking settings.
    /// </summary>
    [HttpGet("GetSettings")]
    [Authorize]
    public async Task<IActionResult> GetSettings()
    {
        try
        {
            var settings = await _errorTrackingService.GetOrCreateSettings();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get error tracking settings");
            return StatusCode(500, new { success = false, error = "Failed to retrieve settings", details = ex.Message, innerException = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Updates error tracking settings.
    /// </summary>
    [HttpPost("UpdateSettings")]
    [Authorize]
    public async Task<IActionResult> UpdateSettings([FromBody] ErrorTrackingSettings settings)
    {
        try
        {
            var success = await _errorTrackingService.UpdateSettings(settings);

            if (!success)
                return NotFound(new { success = false, error = "Settings not found" });

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update error tracking settings");
            return StatusCode(500, new { success = false, error = "Failed to update settings" });
        }
    }

    /// <summary>
    /// Gets error statistics for the dashboard.
    /// </summary>
    [HttpGet("GetStats")]
    [Authorize]
    public async Task<IActionResult> GetStats([FromQuery] int hours = 24)
    {
        try
        {
            var cutoffDate = DateTimeOffset.UtcNow.AddHours(-hours);

            // This could be optimized with a dedicated service method
            var recentErrors = await _errorTrackingService.GetErrorGroups(new ErrorGroupQueryDto
            {
                StartDate = cutoffDate,
                Page = 1,
                PageSize = 1000
            });

            var stats = new
            {
                totalErrors = recentErrors.TotalCount,
                uniqueTypes = recentErrors.ErrorGroups.Select(g => g.ErrorType).Distinct().Count(),
                unresolvedCount = recentErrors.ErrorGroups.Count(g => !g.IsResolved),
                resolvedCount = recentErrors.ErrorGroups.Count(g => g.IsResolved),
                errorsBySource = recentErrors.ErrorGroups.GroupBy(g => g.Source)
                    .Select(g => new { source = g.Key.ToString(), count = g.Count() }),
                errorsByStatusCode = recentErrors.ErrorGroups.GroupBy(g => g.StatusCode)
                    .Select(g => new { statusCode = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get error stats");
            return StatusCode(500, new { success = false, error = "Failed to retrieve statistics" });
        }
    }
}

/// <summary>
/// Request model for marking an error as resolved.
/// </summary>
public class ResolveErrorRequest
{
    public Guid ErrorGroupId { get; set; }
    public string ResolutionNotes { get; set; }
}
