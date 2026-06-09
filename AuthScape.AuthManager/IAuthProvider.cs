namespace AuthScape.AuthManager;

/// <summary>
/// Marker contract every authentication provider implements. Identifies which provider
/// produced a given identity and how the provider relates to AuthScape's token pipeline.
/// </summary>
public interface IAuthProvider
{
    /// <summary>Stable identifier for this provider (e.g. "openiddict", "keycloak"). Used to tag
    /// <see cref="AuthScapeIdentity.ProviderId"/> and to disambiguate token validators.</summary>
    string ProviderId { get; }

    /// <summary>Whether this provider issues tokens (OpenIddict mode) or only validates tokens
    /// issued by an external IdP (Keycloak mode).</summary>
    AuthProviderMode Mode { get; }
}

/// <summary>
/// Determines where token issuance happens in the AuthScape pipeline.
/// </summary>
public enum AuthProviderMode
{
    /// <summary>AuthScape (via OpenIddict) issues tokens. External identity sources feed
    /// INTO the OpenIddict authorization flow.</summary>
    Issuing,

    /// <summary>An external IdP (e.g. Keycloak) issues tokens directly. AuthScape only
    /// validates incoming bearer tokens and maps claims onto an AuthScapeUser.</summary>
    Validating
}
