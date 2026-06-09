namespace AuthScape.AccountLinking.Models;

/// <summary>
/// Returned by IAccountLinkingService.ResolveAsync. Communicates both the resolved AppUser id
/// (when applicable) and what happened, so the caller can render the right UX.
/// </summary>
public class AccountLinkResult
{
    /// <summary>The resolved AppUser id, or null when <see cref="Outcome"/> is PendingManualVerification.</summary>
    public long? AppUserId { get; set; }

    public AccountLinkOutcome Outcome { get; set; }

    /// <summary>
    /// When non-null, the caller should redirect or render this message — typically a verification link
    /// that was emailed to the user, or a notice that they need to confirm their email.
    /// </summary>
    public string? UserFacingMessage { get; set; }

    /// <summary>The audit log row id, for tracing.</summary>
    public long? AuditLogId { get; set; }

    public static AccountLinkResult Created(long appUserId, long auditLogId) => new()
    {
        AppUserId = appUserId,
        Outcome = AccountLinkOutcome.NewUserCreated,
        AuditLogId = auditLogId
    };

    public static AccountLinkResult Linked(long appUserId, long auditLogId) => new()
    {
        AppUserId = appUserId,
        Outcome = AccountLinkOutcome.LinkedToExistingUser,
        AuditLogId = auditLogId
    };

    public static AccountLinkResult AlreadyLinked(long appUserId, long auditLogId) => new()
    {
        AppUserId = appUserId,
        Outcome = AccountLinkOutcome.ReturnedExistingLink,
        AuditLogId = auditLogId
    };

    public static AccountLinkResult PendingVerification(long auditLogId, string userMessage) => new()
    {
        AppUserId = null,
        Outcome = AccountLinkOutcome.PendingManualVerification,
        AuditLogId = auditLogId,
        UserFacingMessage = userMessage
    };

    public static AccountLinkResult UnverifiedEmailNewUser(long appUserId, long auditLogId) => new()
    {
        AppUserId = appUserId,
        Outcome = AccountLinkOutcome.EmailUnverifiedNewUserCreated,
        AuditLogId = auditLogId
    };
}
