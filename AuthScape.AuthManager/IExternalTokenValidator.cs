namespace AuthScape.AuthManager;

/// <summary>
/// Validates bearer tokens issued by an external IdP. Used in <see cref="AuthProviderMode.Validating"/>
/// where AuthScape does not issue tokens itself (e.g. Keycloak realm tokens consumed by AuthScape APIs).
/// </summary>
public interface IExternalTokenValidator : IAuthProvider
{
    /// <summary>Fast-path check: does this validator recognize the token as belonging to its IdP?
    /// Implementations typically inspect the issuer claim without performing signature validation.</summary>
    Task<bool> CanHandleAsync(string token, CancellationToken ct = default);

    /// <summary>Performs full validation (signature, issuer, audience, expiry) and maps the resulting
    /// claims to an <see cref="AuthScapeIdentity"/>. Throws if validation fails.</summary>
    Task<AuthScapeIdentity> ValidateAndMapAsync(string token, CancellationToken ct = default);
}
