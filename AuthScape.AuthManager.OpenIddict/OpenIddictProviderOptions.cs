namespace AuthScape.AuthManager.OpenIddict;

/// <summary>
/// Settings exposed to the host when registering OpenIddict as the AuthScape provider. Mirrors
/// the knobs currently set inside <c>AuthScape.IDP.AuthenticationManager</c> so the host has a
/// single place to tune token issuance.
/// </summary>
public class OpenIddictProviderOptions
{
    /// <summary>SHA-1 thumbprint of the X.509 cert used to sign access/identity tokens in non-dev
    /// environments. Ignored in Development (a dev cert is used). Pulled by AuthenticationManager
    /// from the Windows cert store or Linux base64 env vars.</summary>
    public string? SigningCertificateThumbprint { get; set; }

    /// <summary>SHA-1 thumbprint of the X.509 cert used to encrypt access tokens.</summary>
    public string? EncryptionCertificateThumbprint { get; set; }

    /// <summary>Access token lifetime. Defaults to 1 hour to match the current configuration.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Whether refresh tokens are issued. Default: true.</summary>
    public bool AllowRefreshTokens { get; set; } = true;

    /// <summary>Whether the client credentials flow is enabled (server-to-server). Default: true.</summary>
    public bool AllowClientCredentials { get; set; } = true;

    /// <summary>
    /// Issuer URL of the OpenIddict server (typically the AuthScape IDP, e.g. "https://localhost:44303/").
    /// Required when <see cref="UseIntrospection"/> is true.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Audience this API accepts in incoming tokens. Required when <see cref="UseIntrospection"/> is true.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// When true, the API validates incoming tokens by calling the IDP's RFC 7662 introspection
    /// endpoint instead of validating them locally. Set this on the API; leave it false on the IDP host.
    /// </summary>
    public bool UseIntrospection { get; set; }

    /// <summary>
    /// Client id used when calling the IDP's introspection endpoint. Only consulted when
    /// <see cref="UseIntrospection"/> is true.
    /// </summary>
    public string? IntrospectionClientId { get; set; }

    /// <summary>
    /// Client secret paired with <see cref="IntrospectionClientId"/>. Only consulted when
    /// <see cref="UseIntrospection"/> is true.
    /// </summary>
    public string? IntrospectionClientSecret { get; set; }
}
