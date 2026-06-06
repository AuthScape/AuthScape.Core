using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthScape.AuthManager.Keycloak;

/// <summary>
/// Runs after JwtBearer has cryptographically validated a Keycloak token. Converts the validated
/// principal into an <see cref="AuthScapeIdentity"/>, provisions/syncs the matching
/// <c>AuthScapeUser</c>, and enriches the principal with AuthScape role claims so downstream
/// <c>[Authorize(Roles="...")]</c> attributes use AuthScape role names — not raw Keycloak ones.
/// </summary>
public sealed class KeycloakClaimsTransformation : IClaimsTransformation
{
    private readonly KeycloakProviderOptions options;
    private readonly IClaimsNormalizer normalizer;
    private readonly IServiceProvider services;

    public KeycloakClaimsTransformation(
        IOptions<KeycloakProviderOptions> options,
        IEnumerable<IClaimsNormalizer> normalizers,
        IServiceProvider services)
    {
        this.options = options.Value;
        this.normalizer = normalizers.First(n =>
            string.Equals(n.ProviderId, "keycloak", StringComparison.OrdinalIgnoreCase));
        this.services = services;
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity claimsIdentity || !claimsIdentity.IsAuthenticated)
            return principal!;

        // Idempotency guard: this transformation can run multiple times per request. Skip work if
        // we've already enriched this principal in this request scope.
        const string MarkerClaim = "authscape:transformed";
        if (claimsIdentity.HasClaim(MarkerClaim, "1"))
            return principal;

        var external = ToExternalIdentity(principal);
        var identity = normalizer.Normalize(external);

        // Provision / sync the AppUser using whichever IUserProvisioningService is registered.
        using var scope = services.CreateScope();
        var provisioning = scope.ServiceProvider.GetService<IUserProvisioningService>();
        if (provisioning != null)
        {
            var appUserId = await provisioning.EnsureUserAsync(identity, options.AutoProvision);
            if (appUserId.HasValue)
            {
                identity.AppUserId = appUserId;
                claimsIdentity.AddClaim(new Claim("authscape:userId", appUserId.Value.ToString()));
            }
        }

        // Surface AuthScape role names alongside whatever JwtBearer already added.
        foreach (var role in identity.Roles)
        {
            if (!claimsIdentity.HasClaim(ClaimTypes.Role, role) && !claimsIdentity.HasClaim("role", role))
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        claimsIdentity.AddClaim(new Claim(MarkerClaim, "1"));
        return principal;
    }

    private static ExternalIdentity ToExternalIdentity(ClaimsPrincipal principal)
    {
        var external = new ExternalIdentity { ProviderId = "keycloak" };
        foreach (var c in principal.Claims)
        {
            if (string.Equals(c.Type, "sub", StringComparison.OrdinalIgnoreCase))
                external.Sub = c.Value;
            if (!external.RawClaims.ContainsKey(c.Type))
                external.RawClaims[c.Type] = c.Value;
        }
        return external;
    }
}
