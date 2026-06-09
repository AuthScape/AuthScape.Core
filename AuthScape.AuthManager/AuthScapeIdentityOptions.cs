namespace AuthScape.AuthManager;

/// <summary>
/// Top-level configuration for the AuthScape identity pipeline. Provider-specific options
/// (OpenIddict, Keycloak, etc.) live in their own option types and are configured via the
/// builder extensions.
/// </summary>
public class AuthScapeIdentityOptions
{
    /// <summary>Determines whether AuthScape itself issues tokens (Issuing) or only validates
    /// tokens minted by an external IdP (Validating). Set implicitly by UseOpenIddict()/UseKeycloak().</summary>
    public AuthProviderMode Mode { get; set; } = AuthProviderMode.Issuing;

    /// <summary>Provider id used when callers do not specify one (e.g. the default federation link
    /// on a generic login page).</summary>
    public string DefaultProviderId { get; set; } = "openiddict";

    /// <summary>In <see cref="AuthProviderMode.Validating"/>, the audiences AuthScape APIs accept on
    /// incoming bearer tokens. Ignored in Issuing mode.</summary>
    public IList<string> ValidAudiences { get; set; } = new List<string>();

    /// <summary>In <see cref="AuthProviderMode.Validating"/>, the external IdP's authority URL used
    /// for OIDC discovery (JWKS, issuer). Ignored in Issuing mode.</summary>
    public string? ExternalAuthority { get; set; }

    /// <summary>If true (default), unknown (ProviderId, ExternalSub) pairs auto-create an AuthScapeUser
    /// on first login. If false, the request is rejected and an administrator must pre-provision.</summary>
    public bool AutoProvisionUsers { get; set; } = true;
}
