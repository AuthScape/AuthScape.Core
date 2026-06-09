using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;

namespace AuthScape.AuthManager.Keycloak;

/// <summary>
/// <see cref="IExternalTokenValidator"/> wrapper around JwtBearer's validated principal. The
/// cryptographic work (signature, JWKS rotation, expiry) is delegated to ASP.NET's JwtBearer
/// middleware — this class only handles "is this our IdP?" identification and translates the
/// validated principal into an <see cref="AuthScapeIdentity"/> via the registered normalizer.
/// </summary>
public sealed class KeycloakTokenValidator : IExternalTokenValidator
{
    private readonly KeycloakProviderOptions options;
    private readonly IClaimsNormalizer normalizer;
    private static readonly JwtSecurityTokenHandler Handler = new();

    public KeycloakTokenValidator(IOptions<KeycloakProviderOptions> options, IEnumerable<IClaimsNormalizer> normalizers)
    {
        this.options = options.Value;
        this.normalizer = normalizers.First(n =>
            string.Equals(n.ProviderId, "keycloak", StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public string ProviderId => "keycloak";

    /// <inheritdoc />
    public AuthProviderMode Mode => AuthProviderMode.Validating;

    /// <inheritdoc />
    public Task<bool> CanHandleAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return Task.FromResult(false);
        if (!Handler.CanReadToken(token)) return Task.FromResult(false);

        try
        {
            var jwt = Handler.ReadJwtToken(token);
            // Match by issuer — JwtBearer's IssuerValidator does the cryptographic check later.
            var issuer = jwt.Issuer;
            var authority = options.Authority?.TrimEnd('/');
            return Task.FromResult(
                !string.IsNullOrEmpty(authority)
                && string.Equals(issuer?.TrimEnd('/'), authority, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Decode-only mapping: assumes JwtBearer has already validated signature/issuer/audience/expiry
    /// before this is invoked from a claims transformation. For ad-hoc token introspection from
    /// outside the JwtBearer pipeline, the caller must validate the token themselves first.
    /// </summary>
    public Task<AuthScapeIdentity> ValidateAndMapAsync(string token, CancellationToken ct = default)
    {
        var jwt = Handler.ReadJwtToken(token);
        var external = new ExternalIdentity { ProviderId = ProviderId, Sub = jwt.Subject };
        foreach (var claim in jwt.Claims)
        {
            // Keep the first value for a given claim name. Nested JSON blobs (realm_access etc.) are
            // already serialized strings on the JWT — they round-trip directly through the dictionary.
            if (!external.RawClaims.ContainsKey(claim.Type))
                external.RawClaims[claim.Type] = claim.Value;
        }

        return Task.FromResult(normalizer.Normalize(external));
    }
}
