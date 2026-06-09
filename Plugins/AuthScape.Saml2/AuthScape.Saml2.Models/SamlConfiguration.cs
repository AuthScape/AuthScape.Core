using AuthScape.AccountLinking.Models;
using System.ComponentModel.DataAnnotations;

namespace AuthScape.Saml2.Models;

/// <summary>
/// Per-tenant SAML 2.0 Service Provider configuration. AuthScape acts as the SP;
/// each row represents one upstream IdP (e.g., a customer's Okta or ADFS).
/// CompanyId = null means a global default; populated = per-tenant.
/// </summary>
public class SamlConfiguration
{
    [Key]
    public long Id { get; set; }

    public long? CompanyId { get; set; }

    public bool IsEnabled { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = "";   // Display name on login button, e.g. "Acme Corp SAML"

    // ---- IdP details ----

    [MaxLength(500)]
    public string IdpEntityId { get; set; } = "";

    /// <summary>Optional URL that returns the IdP's SAML metadata XML. Preferred when available.</summary>
    [MaxLength(1000)]
    public string? IdpMetadataUrl { get; set; }

    /// <summary>SAML SSO endpoint at the IdP (HTTP-Redirect or HTTP-POST). Used when metadata isn't available.</summary>
    [MaxLength(1000)]
    public string? IdpSsoUrl { get; set; }

    /// <summary>Base64 PEM-encoded signing certificate the IdP uses to sign assertions.</summary>
    public string? IdpSigningCertificate { get; set; }

    // ---- SP details ----

    [MaxLength(500)]
    public string SpEntityId { get; set; } = "";

    /// <summary>Where the IdP posts SAML responses. Typically /Saml2/Acs/{Id}.</summary>
    [MaxLength(1000)]
    public string AcsUrl { get; set; } = "";

    public bool WantAssertionsSigned { get; set; } = true;

    /// <summary>JSON: maps AuthScape claim names to SAML attribute names. {"email":"...emailaddress","firstName":"givenname"}</summary>
    [MaxLength(4000)]
    public string? ClaimMappingsJson { get; set; }

    // ---- Metadata staleness controls (security/operational gotcha #1) ----

    /// <summary>Last successfully fetched metadata XML. Read on every login by SamlService — never re-fetched on the wire path.</summary>
    public string? IdpMetadataXml { get; set; }
    public DateTime? MetadataLastRefreshedAt { get; set; }
    public int MetadataConsecutiveFailures { get; set; }
    public int MetadataRefreshIntervalHours { get; set; } = 6;

    // ---- Account linking + email-verified controls (gotchas #2 and #3) ----

    public AccountLinkingPolicy AccountLinkingPolicy { get; set; } = AccountLinkingPolicy.LinkIfEmailVerified;

    /// <summary>SAML attribute name the IdP uses to assert email_verified. If null, EmailIsTrustedFromIdp is consulted.</summary>
    [MaxLength(200)]
    public string? EmailVerifiedAttributeName { get; set; }

    /// <summary>Explicit admin opt-in: treat all emails from this IdP as verified. Use ONLY for IdPs the operator fully controls.</summary>
    public bool EmailIsTrustedFromIdp { get; set; }

    [MaxLength(4000)]
    public string? AdditionalSettings { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
