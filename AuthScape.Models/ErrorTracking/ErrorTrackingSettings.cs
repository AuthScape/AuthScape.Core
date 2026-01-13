using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.ErrorTracking;

/// <summary>
/// Admin-configurable settings for error tracking behavior.
/// Stored in database as a single row configuration.
/// </summary>
public class ErrorTrackingSettings
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    // HTTP Status Code Tracking
    public bool Track400Errors { get; set; }

    public bool Track401Errors { get; set; }

    public bool Track403Errors { get; set; }

    public bool Track404Errors { get; set; }

    public bool Track500Errors { get; set; } = true;

    public bool Track502Errors { get; set; }

    public bool Track503Errors { get; set; }

    /// <summary>
    /// Comma-separated list of additional status codes to track (e.g., "429,409,422")
    /// </summary>
    [MaxLength(200)]
    public string CustomStatusCodes { get; set; } = string.Empty;

    // Frontend Tracking
    public bool EnableFrontendTracking { get; set; } = true;

    /// <summary>
    /// How often (in seconds) the frontend should batch and send errors
    /// </summary>
    public int FrontendBatchIntervalSeconds { get; set; } = 30;

    // Data Retention
    /// <summary>
    /// How long to keep error logs before cleanup (in days)
    /// </summary>
    public int RetentionPeriodDays { get; set; } = 90;

    // Performance Settings
    /// <summary>
    /// Maximum size (in KB) for request/response body capture
    /// </summary>
    public int MaxBodySizeKB { get; set; } = 50;

    /// <summary>
    /// Should we capture request bodies?
    /// </summary>
    public bool CaptureRequestBody { get; set; } = true;

    /// <summary>
    /// Should we capture response bodies?
    /// </summary>
    public bool CaptureResponseBody { get; set; } = true;

    /// <summary>
    /// Should we capture request/response headers?
    /// </summary>
    public bool CaptureHeaders { get; set; } = true;

    /// <summary>
    /// Should we capture performance metrics (response time, memory)?
    /// </summary>
    public bool CapturePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Should we link errors to analytics sessions?
    /// </summary>
    public bool LinkToAnalyticsSessions { get; set; } = true;

    // Sensitive Data
    /// <summary>
    /// Should we automatically redact sensitive data (passwords, tokens, etc.)?
    /// </summary>
    public bool AutoRedactSensitiveData { get; set; } = true;

    /// <summary>
    /// Comma-separated list of field names to redact (e.g., "creditCard,ssn")
    /// </summary>
    [MaxLength(1000)]
    public string CustomRedactionFields { get; set; } = string.Empty;

    // Notification Settings
    /// <summary>
    /// Should admins be notified when critical errors occur?
    /// </summary>
    public bool EnableErrorNotifications { get; set; }

    /// <summary>
    /// Only notify for errors with these status codes (comma-separated, e.g., "500,502,503")
    /// </summary>
    [MaxLength(100)]
    public string NotifyForStatusCodes { get; set; } = string.Empty;

    // Metadata
    public DateTimeOffset Created { get; set; }

    public DateTimeOffset Modified { get; set; }
}
