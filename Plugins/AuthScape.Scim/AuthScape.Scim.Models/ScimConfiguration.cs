using System.ComponentModel.DataAnnotations;

namespace AuthScape.Scim.Models;

/// <summary>
/// Per-tenant SCIM 2.0 endpoint configuration. Each row represents one customer's SCIM
/// integration: its tenant slug, its bearer-token OpenIddict client, and any attribute mappings.
///
/// SCIM is intrinsically tenant-scoped — every user/group operation is performed within a
/// CompanyId — so unlike SAML/LDAP there is no "global" mode.
/// </summary>
public class ScimConfiguration
{
    [Key]
    public long Id { get; set; }

    public long CompanyId { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>URL slug used in the SCIM endpoint path: /scim/v2/{TenantSlug}/Users</summary>
    [MaxLength(100)]
    public string TenantSlug { get; set; } = "";

    /// <summary>
    /// The OpenIddict client_id used by this tenant's IdP to obtain a bearer token via
    /// client_credentials. The client is provisioned with a `scim` scope and a custom
    /// claim `scim_company_id={CompanyId}` so the controllers can scope every operation.
    /// </summary>
    [MaxLength(200)]
    public string OpenIddictClientId { get; set; } = "";

    /// <summary>JSON: maps SCIM extension attributes to AppUser columns. e.g. {"urn:ietf:params:scim:schemas:extension:enterprise:2.0:User:department":"Department"}</summary>
    [MaxLength(4000)]
    public string? AttributeMappingsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}
