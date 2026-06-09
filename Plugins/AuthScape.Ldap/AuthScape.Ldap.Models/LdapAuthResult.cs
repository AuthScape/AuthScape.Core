namespace AuthScape.Ldap.Models;

public class LdapAuthResult
{
    public bool Success { get; set; }

    /// <summary>Populated when Success == false.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Mapped per LdapConfiguration.AttributeMappingsJson. e.g. { "email" → "user@acme.com", "firstName" → "Jane" }.</summary>
    public Dictionary<string, string> Attributes { get; set; } = new();

    /// <summary>The bound user's DN — used as ExternalUserId for account linking (stable, unique per directory).</summary>
    public string? UserDistinguishedName { get; set; }

    public static LdapAuthResult Failed(string reason) => new() { Success = false, FailureReason = reason };

    public static LdapAuthResult Ok(string dn, Dictionary<string, string> attributes) => new()
    {
        Success = true,
        UserDistinguishedName = dn,
        Attributes = attributes
    };
}
