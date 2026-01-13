using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.Models.ErrorTracking;

/// <summary>
/// Represents a group of similar errors for efficient tracking and display.
/// Groups errors by their signature (hash of error type + stack trace + endpoint).
/// </summary>
public class ErrorGroup
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// SHA256 hash of (ErrorType + FirstMeaningfulStackLine + Endpoint)
    /// Used to group duplicate errors together
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string ErrorSignature { get; set; }

    [Required]
    [MaxLength(1000)]
    public string ErrorMessage { get; set; }

    [Required]
    [MaxLength(500)]
    public string ErrorType { get; set; }

    [MaxLength(500)]
    public string Endpoint { get; set; }

    public int StatusCode { get; set; }

    public ErrorSource Source { get; set; }

    /// <summary>
    /// Total number of times this error has occurred
    /// </summary>
    public int OccurrenceCount { get; set; }

    /// <summary>
    /// First time this error was seen
    /// </summary>
    public DateTimeOffset FirstSeen { get; set; }

    /// <summary>
    /// Most recent occurrence of this error
    /// </summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>
    /// Has this error group been marked as resolved?
    /// </summary>
    public bool IsResolved { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public long? ResolvedByUserId { get; set; }

    [MaxLength(2000)]
    public string ResolutionNotes { get; set; }

    /// <summary>
    /// Sample stack trace (from the first occurrence)
    /// </summary>
    public string SampleStackTrace { get; set; }

    /// <summary>
    /// Environment where this error group was first seen
    /// </summary>
    public Stage Environment { get; set; }
}
