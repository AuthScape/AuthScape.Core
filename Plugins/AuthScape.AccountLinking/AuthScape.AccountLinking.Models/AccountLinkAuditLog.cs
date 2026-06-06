using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthScape.AccountLinking.Models;

/// <summary>
/// Append-only record of every account-linking decision. Used for incident investigation
/// (e.g., "did this AppUser get linked to a SAML provider yesterday?") and for SOC2 / compliance
/// audit trails. Never deleted; only inserted.
/// </summary>
public class AccountLinkAuditLog
{
    [Key]
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>The AppUser involved. Null only when <see cref="Outcome"/> is PendingManualVerification.</summary>
    public long? AppUserId { get; set; }

    [MaxLength(100)]
    public string Provider { get; set; } = "";

    [MaxLength(500)]
    public string ExternalUserId { get; set; } = "";

    [MaxLength(320)] // RFC 5321 max email length
    public string? Email { get; set; }

    public bool EmailVerifiedByProvider { get; set; }

    public AccountLinkingPolicy PolicyApplied { get; set; }

    public AccountLinkOutcome Outcome { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public long? CompanyId { get; set; }

    /// <summary>
    /// When Outcome is PendingManualVerification, the verification token issued.
    /// Hashed before storage; do not put a usable token here.
    /// </summary>
    [MaxLength(128)]
    public string? VerificationTokenHash { get; set; }
}
