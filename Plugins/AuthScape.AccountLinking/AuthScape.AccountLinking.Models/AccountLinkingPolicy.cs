namespace AuthScape.AccountLinking.Models;

/// <summary>
/// Determines how a federated identity (SAML, LDAP, OAuth) is matched to an existing AppUser.
/// </summary>
public enum AccountLinkingPolicy
{
    /// <summary>
    /// Strictest: every (provider, externalId) pair gets its own AppUser. Email collisions trigger
    /// an email-confirmation flow that lets the legitimate owner manually link the new login.
    /// Eliminates the email-based account-takeover vector at the cost of more friction.
    /// </summary>
    NeverAutoLink = 0,

    /// <summary>
    /// Default. Auto-link the federated login to an existing AppUser by email ONLY when the
    /// upstream IdP explicitly asserts the email is verified. Otherwise treat as a new user.
    /// </summary>
    LinkIfEmailVerified = 1,

    /// <summary>
    /// Insecure. Auto-link by email match regardless of whether the upstream verified ownership.
    /// Enables account takeover when the IdP doesn't actually verify emails. Requires explicit
    /// admin opt-in (a confirmation checkbox in the admin UI). Reserved for IdPs the operator
    /// fully trusts (e.g., the operator's own corporate ADFS).
    /// </summary>
    AlwaysLinkByEmail = 2
}
