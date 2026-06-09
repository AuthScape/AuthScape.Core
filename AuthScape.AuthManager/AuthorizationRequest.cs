namespace AuthScape.AuthManager;

/// <summary>
/// Parameters passed to <see cref="IIdentityProvider.BuildAuthorizationUrlAsync"/> when starting
/// an OAuth/OIDC authorization-code flow against an upstream provider.
/// </summary>
public class AuthorizationRequest
{
    /// <summary>Target provider (must match one of the registered <see cref="IIdentityProvider"/>s).</summary>
    public string ProviderId { get; set; } = "";

    /// <summary>Callback URL that the upstream provider will redirect back to with the code.</summary>
    public string RedirectUri { get; set; } = "";

    /// <summary>Opaque state token for CSRF protection — must round-trip to the callback unchanged.</summary>
    public string State { get; set; } = "";

    /// <summary>PKCE code challenge derived from a verifier kept on the client side.</summary>
    public string CodeChallenge { get; set; } = "";

    /// <summary>PKCE code challenge method. Always "S256" in modern flows.</summary>
    public string CodeChallengeMethod { get; set; } = "S256";

    /// <summary>Requested OAuth scopes (e.g. "openid", "profile", "email").</summary>
    public IList<string> Scopes { get; set; } = new List<string>();
}

/// <summary>
/// Parameters passed to <see cref="IIdentityProvider.HandleCallbackAsync"/> after the user agent
/// is redirected back from the upstream provider with an authorization code.
/// </summary>
public class AuthorizationCallback
{
    /// <summary>Provider that issued this callback (must match the original AuthorizationRequest).</summary>
    public string ProviderId { get; set; } = "";

    /// <summary>Authorization code returned by the upstream provider.</summary>
    public string Code { get; set; } = "";

    /// <summary>State token from the upstream redirect — caller must verify it matches the request state
    /// before invoking the provider.</summary>
    public string State { get; set; } = "";

    /// <summary>PKCE code verifier paired with the code_challenge sent in the original request.</summary>
    public string CodeVerifier { get; set; } = "";

    /// <summary>The redirect_uri sent in the original request (must match exactly per the spec).</summary>
    public string RedirectUri { get; set; } = "";
}
