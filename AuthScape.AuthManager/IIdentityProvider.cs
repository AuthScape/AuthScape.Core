namespace AuthScape.AuthManager;

/// <summary>
/// Upstream OIDC/OAuth provider that feeds identity into AuthScape's local token issuer
/// (used in <see cref="AuthProviderMode.Issuing"/>). The local OpenIddict server receives
/// the resulting <see cref="ExternalIdentity"/> and mints its own tokens from it.
/// </summary>
public interface IIdentityProvider : IAuthProvider
{
    /// <summary>Build the URL the user agent is redirected to in order to start the authorization
    /// flow at the upstream provider.</summary>
    Task<Uri> BuildAuthorizationUrlAsync(AuthorizationRequest request, CancellationToken ct = default);

    /// <summary>Exchange the authorization callback (code + state) for the upstream provider's
    /// user profile. Returned <see cref="ExternalIdentity"/> is then normalized into an
    /// <see cref="AuthScapeIdentity"/> by <see cref="IClaimsNormalizer"/>.</summary>
    Task<ExternalIdentity> HandleCallbackAsync(AuthorizationCallback callback, CancellationToken ct = default);

    /// <summary>Optionally refresh the upstream user's profile (e.g. to pick up role changes
    /// on the upstream IdP between logins). Returns null when refresh is not supported.</summary>
    Task<ExternalIdentity?> RefreshAsync(string providerUserId, CancellationToken ct = default);

    /// <summary>Best-effort revoke of upstream sessions/tokens for the given user.</summary>
    Task RevokeAsync(string providerUserId, CancellationToken ct = default);
}
