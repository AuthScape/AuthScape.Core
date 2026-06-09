using AuthScape.AccountLinking.Models;

namespace AuthScape.AccountLinking;

/// <summary>
/// Resolves an AppUser for an authenticated external identity, applying the configured
/// AccountLinkingPolicy and emitting an audit log entry. Consumed by SAML, LDAP, and OAuth
/// federation handlers — all federated paths funnel through this single service so the
/// account-takeover protection rules are enforced consistently.
/// </summary>
public interface IAccountLinkingService
{
    /// <summary>
    /// Resolves or creates an AppUser for the given external identity per the policy.
    ///
    /// Behavior matrix (the security-critical part):
    /// <list type="bullet">
    /// <item>If (Provider, ExternalUserId) is already in AspNetUserLogins → return that AppUser.</item>
    /// <item>Else if policy is NeverAutoLink and email collides → emit verification email, return PendingManualVerification.</item>
    /// <item>Else if policy is LinkIfEmailVerified and EmailVerifiedByProvider is true and an AppUser with that email exists → link and return.</item>
    /// <item>Else if policy is LinkIfEmailVerified and EmailVerifiedByProvider is false → create a new AppUser (do NOT search by email).</item>
    /// <item>Else if policy is AlwaysLinkByEmail and an AppUser with that email exists → link and return (insecure path; admin must opt in).</item>
    /// <item>Otherwise → create a new AppUser.</item>
    /// </list>
    /// </summary>
    /// <param name="identity">The external identity claims, populated by the federation handler.</param>
    /// <param name="policy">The linking policy from the per-tenant SAML/LDAP/OAuth config.</param>
    /// <param name="ipAddress">Client IP for audit logging.</param>
    /// <param name="userAgent">Client UA for audit logging.</param>
    Task<AccountLinkResult> ResolveAsync(
        ExternalIdentity identity,
        AccountLinkingPolicy policy,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a deferred link from a NeverAutoLink collision flow. Called when the user
    /// clicks the verification link sent to their existing email address.
    /// </summary>
    /// <param name="auditLogId">The id of the original PendingManualVerification audit row.</param>
    /// <param name="verificationToken">The plaintext token from the email link; will be hash-compared.</param>
    Task<AccountLinkResult> CompleteVerifiedLinkAsync(
        long auditLogId,
        string verificationToken,
        CancellationToken cancellationToken = default);
}
