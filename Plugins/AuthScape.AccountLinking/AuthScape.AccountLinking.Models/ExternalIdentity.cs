namespace AuthScape.AccountLinking.Models;

/// <summary>
/// Provider-agnostic representation of an authenticated identity arriving from an external IdP.
/// SAML, LDAP, and OAuth handlers all populate this and pass it to IAccountLinkingService.ResolveAsync.
/// </summary>
public class ExternalIdentity
{
    /// <summary>
    /// Stable, opaque provider identifier. Examples: "Saml2_12", "Ldap_3", "Google", "Keycloak".
    /// Combined with <see cref="ExternalUserId"/> forms the unique key used by AspNetUserLogins.
    /// </summary>
    public string Provider { get; set; } = "";

    /// <summary>
    /// The provider's stable identifier for the user. SAML NameID, LDAP DN (or objectGUID), OAuth sub claim.
    /// </summary>
    public string ExternalUserId { get; set; } = "";

    public string? Email { get; set; }

    /// <summary>
    /// Whether the upstream IdP asserts that the user controls the email address.
    /// CRITICAL — when false, IAccountLinkingService MUST NOT auto-link by email under
    /// <see cref="AccountLinkingPolicy.LinkIfEmailVerified"/>. False is the safe default.
    /// </summary>
    public bool EmailVerifiedByProvider { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Display name for the provider, e.g. "Acme Corp SAML" — used for audit log and UX.</summary>
    public string? ProviderDisplayName { get; set; }

    /// <summary>Optional tenant scope (Company.Id). When set, linking lookups are restricted to that tenant.</summary>
    public long? CompanyId { get; set; }

    /// <summary>Raw claims from the upstream — preserved for audit trail and downstream consumers.</summary>
    public Dictionary<string, string> RawClaims { get; set; } = new();
}
