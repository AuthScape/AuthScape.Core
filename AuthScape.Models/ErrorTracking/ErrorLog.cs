using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.ErrorTracking;

/// <summary>
/// Represents an individual error occurrence logged in the system.
/// Tracks detailed information about errors from API, IDP, or Frontend.
/// </summary>
public class ErrorLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the error group this belongs to (for grouping duplicate errors)
    /// </summary>
    public Guid? ErrorGroupId { get; set; }

    // Error Details
    [Required]
    [MaxLength(1000)]
    public string ErrorMessage { get; set; }

    [Required]
    [MaxLength(500)]
    public string ErrorType { get; set; }

    public string StackTrace { get; set; }

    // HTTP Context
    public int StatusCode { get; set; }

    [MaxLength(500)]
    public string Endpoint { get; set; }

    [MaxLength(200)]
    public string HttpMethod { get; set; }

    public string RequestBody { get; set; }

    public string ResponseBody { get; set; }

    public bool RequestBodyTruncated { get; set; }

    public bool ResponseBodyTruncated { get; set; }

    [MaxLength(2000)]
    public string QueryString { get; set; }

    [MaxLength(4000)]
    public string RequestHeaders { get; set; }

    [MaxLength(4000)]
    public string ResponseHeaders { get; set; }

    // User Context
    public long? UserId { get; set; }

    [MaxLength(100)]
    public string Username { get; set; }

    [MaxLength(200)]
    public string UserEmail { get; set; }

    // Request Context
    [MaxLength(50)]
    public string IPAddress { get; set; }

    [MaxLength(500)]
    public string UserAgent { get; set; }

    [MaxLength(100)]
    public string Browser { get; set; }

    [MaxLength(100)]
    public string BrowserVersion { get; set; }

    [MaxLength(100)]
    public string OperatingSystem { get; set; }

    [MaxLength(100)]
    public string DeviceType { get; set; }

    // Session Correlation (link to analytics)
    public Guid? AnalyticsSessionId { get; set; }

    // Environment Information
    public Stage Environment { get; set; }

    [MaxLength(100)]
    public string DatabaseProvider { get; set; }

    [MaxLength(100)]
    public string MachineName { get; set; }

    [MaxLength(100)]
    public string ApplicationVersion { get; set; }

    // Performance Metrics
    public long? ResponseTimeMs { get; set; }

    public long? MemoryUsageMB { get; set; }

    public int? ThreadCount { get; set; }

    // Source Information
    public ErrorSource Source { get; set; }

    [MaxLength(200)]
    public string ComponentName { get; set; }

    // Resolution Tracking
    public bool IsResolved { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public long? ResolvedByUserId { get; set; }

    [MaxLength(2000)]
    public string ResolutionNotes { get; set; }

    // Metadata
    public string AdditionalMetadata { get; set; }

    public DateTimeOffset Created { get; set; }

    // Navigation properties removed to avoid circular dependencies
    // Use Include() with explicit loading in queries if needed
}

/// <summary>
/// Source of the error (which application/layer)
/// </summary>
public enum ErrorSource
{
    API = 1,
    IDP = 2,
    Frontend = 3
}
