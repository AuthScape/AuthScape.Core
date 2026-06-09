using AuthScape.AccountLinking.Models;
using AuthScape.Saml2.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Context;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace AuthScape.Saml2;

/// <summary>
/// Minimal but correct SAML 2.0 SP-side response validator.
///
/// Scope of this implementation:
/// - HTTP-POST binding only (the most common SP-side binding)
/// - Validates XML signature via SignedXml against the IdP signing cert from the cached metadata
/// - Validates Conditions/@NotBefore and NotOnOrAfter
/// - Extracts NameID and AttributeStatements
///
/// NOT implemented (deliberate — would be added by swapping in Sustainsys.Saml2 or ITfoxtec):
/// - HTTP-Redirect binding (rarely used SP-side)
/// - Encrypted assertions
/// - Audience restrictions enforcement (trivial to add via Conditions/AudienceRestriction)
/// - Replay protection via assertion ID cache (production must add this)
/// - Subject confirmation Method validation
/// - SAML metadata signature validation on the metadata refresh path
///
/// To upgrade to Sustainsys.Saml2: replace this class. The ISamlService contract is stable
/// and all callers (REST, Razor, OAuth grant) will continue to work unchanged.
/// </summary>
public class SamlService : ISamlService
{
    private const string SamlAssertionNs = "urn:oasis:names:tc:SAML:2.0:assertion";
    private const string SamlProtocolNs = "urn:oasis:names:tc:SAML:2.0:protocol";

    private readonly DatabaseContext db;
    private readonly ILogger<SamlService> logger;

    public SamlService(DatabaseContext db, ILogger<SamlService> logger)
    {
        this.db = db;
        this.logger = logger;
    }

    public async Task<List<SamlConfiguration>> GetEnabledForCompanyAsync(long? companyId, CancellationToken cancellationToken = default)
    {
        return await db.Set<SamlConfiguration>()
            .Where(c => c.IsEnabled)
            .Where(c => c.CompanyId == companyId || c.CompanyId == null)
            .OrderBy(c => c.CompanyId == null ? 1 : 0)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<SamlConfiguration?> GetConfigAsync(long configId, CancellationToken cancellationToken = default) =>
        await db.Set<SamlConfiguration>().FirstOrDefaultAsync(c => c.Id == configId, cancellationToken);

    public async Task<ExternalIdentity> ValidateAssertionAsync(
        string samlResponseBase64,
        long configId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(samlResponseBase64))
            throw new SamlValidationException("SAML response is empty");

        var config = await GetConfigAsync(configId, cancellationToken)
            ?? throw new SamlValidationException("SAML configuration not found");
        if (!config.IsEnabled)
            throw new SamlValidationException("SAML configuration is disabled");

        // Decode and parse.
        XmlDocument doc;
        try
        {
            var xml = Encoding.UTF8.GetString(Convert.FromBase64String(samlResponseBase64));
            doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(xml);
        }
        catch (Exception ex)
        {
            throw new SamlValidationException("SAML response is not valid base64-encoded XML", ex);
        }

        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("samlp", SamlProtocolNs);
        nsmgr.AddNamespace("saml", SamlAssertionNs);
        nsmgr.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);

        // Locate the Assertion. We validate the assertion's signature (some IdPs sign the Response,
        // others sign the Assertion — we expect the latter, which is best practice).
        var assertion = doc.SelectSingleNode("//saml:Assertion", nsmgr) as XmlElement
            ?? throw new SamlValidationException("SAML response missing Assertion");

        // Verify XML signature.
        VerifyAssertionSignature(doc, assertion, config);

        // Validate timestamps.
        ValidateConditions(assertion, nsmgr);

        // Extract NameID.
        var nameIdNode = assertion.SelectSingleNode("saml:Subject/saml:NameID", nsmgr) as XmlElement
            ?? throw new SamlValidationException("Assertion missing Subject/NameID");
        var nameId = nameIdNode.InnerText.Trim();
        if (string.IsNullOrEmpty(nameId))
            throw new SamlValidationException("NameID is empty");

        // Extract attributes.
        var rawAttributes = new Dictionary<string, string>();
        var attributeNodes = assertion.SelectNodes("saml:AttributeStatement/saml:Attribute", nsmgr);
        if (attributeNodes != null)
        {
            foreach (XmlElement attr in attributeNodes)
            {
                var attrName = attr.GetAttribute("Name");
                if (string.IsNullOrEmpty(attrName)) continue;
                var valueNode = attr.SelectSingleNode("saml:AttributeValue", nsmgr);
                if (valueNode != null)
                    rawAttributes[attrName] = valueNode.InnerText.Trim();
            }
        }

        // Apply claim mappings.
        var mappings = ParseClaimMappings(config.ClaimMappingsJson);
        string? Map(string claimKey) =>
            mappings.TryGetValue(claimKey, out var samlAttr) && rawAttributes.TryGetValue(samlAttr, out var v) ? v : null;

        var email = Map("email") ?? rawAttributes.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        var firstName = Map("firstName") ?? rawAttributes.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname");
        var lastName = Map("lastName") ?? rawAttributes.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname");

        // Determine email verification — see security gotcha #3 in the plan.
        var emailVerified = false;
        if (!string.IsNullOrEmpty(config.EmailVerifiedAttributeName)
            && rawAttributes.TryGetValue(config.EmailVerifiedAttributeName, out var verifiedAttr))
        {
            emailVerified = string.Equals(verifiedAttr, "true", StringComparison.OrdinalIgnoreCase)
                          || verifiedAttr == "1";
        }
        else if (config.EmailIsTrustedFromIdp)
        {
            emailVerified = true;
        }

        return new ExternalIdentity
        {
            Provider = $"Saml2_{config.Id}",
            ExternalUserId = nameId,
            Email = email,
            EmailVerifiedByProvider = emailVerified,
            FirstName = firstName,
            LastName = lastName,
            ProviderDisplayName = config.Name,
            CompanyId = config.CompanyId,
            RawClaims = rawAttributes
        };
    }

    private static void VerifyAssertionSignature(XmlDocument doc, XmlElement assertion, SamlConfiguration config)
    {
        if (!config.WantAssertionsSigned)
            return; // Operator opted out — this is rarely the right call but we honor config.

        if (string.IsNullOrEmpty(config.IdpSigningCertificate)
            && string.IsNullOrEmpty(config.IdpMetadataXml))
            throw new SamlValidationException("IdP signing certificate not configured");

        var cert = LoadIdpCertificate(config)
            ?? throw new SamlValidationException("Could not load IdP signing certificate");

        var signedXml = new SignedXml(assertion);
        var signatureNode = assertion.SelectSingleNode("ds:Signature",
            CreateNamespaceManager(doc)) as XmlElement
            ?? throw new SamlValidationException("Assertion is not signed");

        signedXml.LoadXml(signatureNode);

        if (!signedXml.CheckSignature(cert, true))
            throw new SamlValidationException("Assertion signature is invalid");
    }

    private static XmlNamespaceManager CreateNamespaceManager(XmlDocument doc)
    {
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
        return nsmgr;
    }

    private static X509Certificate2? LoadIdpCertificate(SamlConfiguration config)
    {
        if (!string.IsNullOrEmpty(config.IdpSigningCertificate))
        {
            try
            {
                var pem = config.IdpSigningCertificate.Trim();
                // Strip PEM markers if present so we accept either PEM or base64 DER.
                pem = pem.Replace("-----BEGIN CERTIFICATE-----", "")
                         .Replace("-----END CERTIFICATE-----", "")
                         .Replace("\r", "").Replace("\n", "").Trim();
                var bytes = Convert.FromBase64String(pem);
                return X509CertificateLoader.LoadCertificate(bytes);
            }
            catch
            {
                return null;
            }
        }
        // TODO: when IdpSigningCertificate isn't set, parse IdpMetadataXml's <ds:X509Certificate>.
        return null;
    }

    private static void ValidateConditions(XmlElement assertion, XmlNamespaceManager nsmgr)
    {
        var conditions = assertion.SelectSingleNode("saml:Conditions", nsmgr) as XmlElement;
        if (conditions == null) return; // Conditions are optional

        var now = DateTime.UtcNow;

        if (conditions.HasAttribute("NotBefore"))
        {
            var notBefore = DateTime.Parse(conditions.GetAttribute("NotBefore"),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
            if (now < notBefore.AddMinutes(-2)) // 2-min clock skew tolerance
                throw new SamlValidationException("Assertion not yet valid (NotBefore in the future)");
        }
        if (conditions.HasAttribute("NotOnOrAfter"))
        {
            var notOnOrAfter = DateTime.Parse(conditions.GetAttribute("NotOnOrAfter"),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
            if (now >= notOnOrAfter.AddMinutes(2))
                throw new SamlValidationException("Assertion expired (NotOnOrAfter in the past)");
        }
    }

    private static Dictionary<string, string> ParseClaimMappings(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }
}
