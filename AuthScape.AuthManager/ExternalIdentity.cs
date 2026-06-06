namespace AuthScape.AuthManager;

/// <summary>
/// Intermediate identity payload returned by an upstream <see cref="IIdentityProvider"/> before
/// it is normalized into the canonical <see cref="AuthScapeIdentity"/>. Field names mirror standard
/// OIDC claim names so providers can fill it in with minimal mapping.
/// </summary>
public class ExternalIdentity
{
    /// <summary>Which provider produced this payload.</summary>
    public string ProviderId { get; set; } = "";

    /// <summary>Provider 'sub' claim — the upstream stable user identifier.</summary>
    public string Sub { get; set; } = "";

    /// <summary>Email claim when present in the upstream profile.</summary>
    public string? Email { get; set; }

    /// <summary>Whether the upstream IdP considers the email verified.</summary>
    public bool EmailVerified { get; set; }

    /// <summary>Display name as supplied by the upstream IdP (e.g. preferred_username, name).</summary>
    public string? Name { get; set; }

    /// <summary>Given name claim when present.</summary>
    public string? GivenName { get; set; }

    /// <summary>Family name claim when present.</summary>
    public string? FamilyName { get; set; }

    /// <summary>Profile picture URL when present.</summary>
    public string? Picture { get; set; }

    /// <summary>Full claim bag from the upstream provider, including nested structures rendered
    /// as JSON strings for non-standard claims (e.g. Keycloak's realm_access).</summary>
    public IDictionary<string, string> RawClaims { get; set; }
        = new Dictionary<string, string>();
}
