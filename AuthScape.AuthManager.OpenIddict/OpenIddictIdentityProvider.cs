namespace AuthScape.AuthManager.OpenIddict;

/// <summary>
/// <see cref="IIdentityProvider"/> adapter that represents AuthScape's local OpenIddict server.
/// </summary>
/// <remarks>
/// OpenIddict is an <see cref="AuthProviderMode.Issuing"/> provider: it does not federate out to
/// an upstream IdP to fetch a profile — it issues tokens for local AppUsers signed in through the
/// scaffolded Identity login UI. The <see cref="IIdentityProvider"/> contract is therefore mostly
/// a no-op here; the work happens inside the existing <c>AuthorizationController</c>.
/// </remarks>
public sealed class OpenIddictIdentityProvider : IIdentityProvider
{
    /// <inheritdoc />
    public string ProviderId => "openiddict";

    /// <inheritdoc />
    public AuthProviderMode Mode => AuthProviderMode.Issuing;

    /// <inheritdoc />
    public Task<Uri> BuildAuthorizationUrlAsync(AuthorizationRequest request, CancellationToken ct = default)
    {
        // OpenIddict's authorization endpoint is local; callers should redirect to /connect/authorize
        // directly. Returning a sentinel URL keeps the contract honored without inventing a flow that
        // doesn't exist for local users.
        return Task.FromResult(new Uri("/connect/authorize", UriKind.Relative));
    }

    /// <inheritdoc />
    public Task<ExternalIdentity> HandleCallbackAsync(AuthorizationCallback callback, CancellationToken ct = default)
    {
        // For local OpenIddict users there is no external callback to handle — the AuthorizationController
        // signs the user in and the token endpoint mints tokens directly.
        throw new NotSupportedException(
            "OpenIddict is a local issuing provider; there is no external callback. " +
            "Local users authenticate via the Identity login UI at /Identity/Account/Login.");
    }

    /// <inheritdoc />
    public Task<ExternalIdentity?> RefreshAsync(string providerUserId, CancellationToken ct = default)
        => Task.FromResult<ExternalIdentity?>(null);

    /// <inheritdoc />
    public Task RevokeAsync(string providerUserId, CancellationToken ct = default)
        => Task.CompletedTask;
}
