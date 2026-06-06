namespace AuthScape.AuthManager;

/// <summary>
/// Resolves an incoming external identity (Keycloak token, OAuth login, etc.) to a local AppUser
/// row. Provisions a new AppUser on first sight, syncs cached profile fields on subsequent logins,
/// and returns the AppUser id so the claims pipeline can attach it to the principal.
/// </summary>
public interface IUserProvisioningService
{
    /// <summary>
    /// Find the AppUser matching this external identity, or provision one if it does not exist.
    /// Returns the AppUser primary key, or <c>null</c> when no user could be resolved and
    /// <paramref name="autoProvision"/> is false.
    /// </summary>
    Task<long?> EnsureUserAsync(AuthScapeIdentity identity, bool autoProvision, CancellationToken ct = default);
}
