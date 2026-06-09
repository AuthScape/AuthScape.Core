using AuthScape.Ldap.Models;

namespace AuthScape.Ldap;

public interface ILdapAuthService
{
    /// <summary>
    /// Looks up the LdapConfiguration that should handle the given login. Resolution order:
    /// (1) explicit configId from the caller, (2) match against EmailDomainHint, (3) global fallback (CompanyId == null).
    /// Returns null if none matches.
    /// </summary>
    Task<LdapConfiguration?> ResolveConfigAsync(string emailOrUsername, long? companyId, long? explicitConfigId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs an LDAP bind with the given credentials. On success, returns the bound user's DN
    /// and any mapped attributes per AttributeMappingsJson. On failure, returns FailureReason
    /// (no leaking of LDAP error codes; safe to display).
    /// </summary>
    Task<LdapAuthResult> AuthenticateAsync(string username, string password, long configId, CancellationToken cancellationToken = default);
}
