using AuthScape.AccountLinking.Models;
using System.ComponentModel.DataAnnotations;

namespace AuthScape.Ldap.Models;

/// <summary>
/// Per-tenant LDAP / Active Directory configuration. CompanyId = null means a global default
/// (rare; usually each customer brings their own AD).
/// </summary>
public class LdapConfiguration
{
    [Key]
    public long Id { get; set; }

    public long? CompanyId { get; set; }

    public bool IsEnabled { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = "";   // Display name e.g. "Acme Corp AD"

    /// <summary>e.g. ldaps://ad.acme.local:636 or ldap://ad.acme.local:389</summary>
    [MaxLength(500)]
    public string ServerUrl { get; set; } = "";

    public bool UseStartTls { get; set; }

    /// <summary>
    /// Bind DN template with {username} placeholder.
    /// Examples:
    ///   "{username}@acme.local"   (UPN style)
    ///   "uid={username},ou=Users,dc=acme,dc=local"   (DN style)
    /// </summary>
    [MaxLength(500)]
    public string BindDnTemplate { get; set; } = "";

    /// <summary>Optional — for pre-bind directory search to resolve usernames or read attributes.</summary>
    [MaxLength(500)]
    public string? SearchBase { get; set; }

    /// <summary>Optional — LDAP filter for the user lookup. {username} is replaced. e.g. "(sAMAccountName={username})"</summary>
    [MaxLength(500)]
    public string? UserFilter { get; set; }

    /// <summary>Optional — service-account DN used for pre-bind searching when needed.</summary>
    [MaxLength(500)]
    public string? ServiceAccountDn { get; set; }

    /// <summary>Encrypted at rest via DataProtection.</summary>
    [MaxLength(2000)]
    public string? ServiceAccountPasswordEncrypted { get; set; }

    /// <summary>JSON: {"email":"mail","firstName":"givenName","lastName":"sn"}</summary>
    [MaxLength(2000)]
    public string? AttributeMappingsJson { get; set; }

    /// <summary>Routing hint: emails ending in any of these domains route to this LDAP config. Comma-separated.</summary>
    [MaxLength(1000)]
    public string? EmailDomainHint { get; set; }

    public AccountLinkingPolicy AccountLinkingPolicy { get; set; } = AccountLinkingPolicy.LinkIfEmailVerified;

    /// <summary>
    /// Comma-separated list of domains the operator certifies the LDAP authoritatively owns.
    /// Email addresses ending in these domains are treated as verified for account-linking purposes.
    /// </summary>
    [MaxLength(1000)]
    public string? TrustedEmailDomains { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
