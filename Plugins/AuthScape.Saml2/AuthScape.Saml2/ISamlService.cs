using AuthScape.AccountLinking.Models;
using AuthScape.Saml2.Models;

namespace AuthScape.Saml2;

/// <summary>
/// Protocol-agnostic SAML 2.0 Service Provider operations. The Razor login flow,
/// the REST controller, and the OAuth bearer-grant handler all funnel through this
/// service — it is the single place SAML responses are validated and converted to
/// AuthScape identities.
/// </summary>
public interface ISamlService
{
    Task<List<SamlConfiguration>> GetEnabledForCompanyAsync(long? companyId, CancellationToken cancellationToken = default);

    Task<SamlConfiguration?> GetConfigAsync(long configId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a SAML Response (base64-encoded XML) against the IdP's signing certificate
    /// and returns the resulting external identity. Performs:
    /// <list type="bullet">
    /// <item>Base64 decode + XML parse</item>
    /// <item>XML signature validation against the IdP signing cert from the cached metadata</item>
    /// <item>Conditions / timestamp validation (NotBefore, NotOnOrAfter, audience)</item>
    /// <item>Claim extraction per SamlConfiguration.ClaimMappingsJson</item>
    /// <item>Email-verified determination per EmailVerifiedAttributeName / EmailIsTrustedFromIdp</item>
    /// </list>
    /// Throws <see cref="SamlValidationException"/> on any failure.
    /// </summary>
    Task<ExternalIdentity> ValidateAssertionAsync(string samlResponseBase64, long configId, CancellationToken cancellationToken = default);
}
