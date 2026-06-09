namespace AuthScape.AuthManager;

/// <summary>
/// Canonical, provider-agnostic identity produced by any AuthScape provider after
/// validation/normalization. Downstream code (controllers, services, plugins) consumes
/// this — never the raw provider claims — so it does not care which IdP is active.
/// </summary>
public class AuthScapeIdentity
{
    /// <summary>Which provider produced this identity (e.g. "openiddict", "keycloak").</summary>
    public string ProviderId { get; set; } = "";

    /// <summary>The external provider's stable user identifier (typically the JWT 'sub' claim).</summary>
    public string ExternalSub { get; set; } = "";

    /// <summary>Primary email address, when known.</summary>
    public string? Email { get; set; }

    /// <summary>Whether the IdP considers <see cref="Email"/> verified.</summary>
    public bool EmailVerified { get; set; }

    /// <summary>Display name (often falls back to "{GivenName} {FamilyName}" or preferred_username).</summary>
    public string? DisplayName { get; set; }

    /// <summary>Given (first) name when available.</summary>
    public string? GivenName { get; set; }

    /// <summary>Family (last) name when available.</summary>
    public string? FamilyName { get; set; }

    /// <summary>Profile picture URL when available.</summary>
    public string? PictureUrl { get; set; }

    /// <summary>AuthScape's internal AppUser id after provisioning. Null until
    /// <see cref="IUserProvisioningService"/> has run for this identity.</summary>
    public long? AppUserId { get; set; }

    /// <summary>Company id (multi-tenant scope) the user belongs to. Null for platform-wide users.</summary>
    public long? CompanyId { get; set; }

    /// <summary>AuthScape application roles (post role-mapping, not the raw provider role names).</summary>
    public IList<string> Roles { get; set; } = new List<string>();

    /// <summary>AuthScape application permissions resolved for this identity.</summary>
    public IList<string> Permissions { get; set; } = new List<string>();

    /// <summary>Raw claim bag for anything not mapped to a named field above — preserved so
    /// downstream consumers can read provider-specific extras without re-validating the token.</summary>
    public IDictionary<string, string> AdditionalClaims { get; set; }
        = new Dictionary<string, string>();
}
