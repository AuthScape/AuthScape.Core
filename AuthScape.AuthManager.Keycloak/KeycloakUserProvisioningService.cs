using AuthScape.Models.Users;
using CoreBackpack.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Context;

namespace AuthScape.AuthManager.Keycloak;

/// <summary>
/// Resolves a Keycloak-issued identity to a local <see cref="AppUser"/> row. Looks up by
/// (ExternalProvider, ExternalSub) first, falls back to Email, and provisions a new AppUser via
/// <see cref="UserManager{TUser}"/> when no match is found and AutoProvision is on.
/// </summary>
public sealed class KeycloakUserProvisioningService : IUserProvisioningService
{
    private readonly DatabaseContext db;
    private readonly UserManager<AppUser> userManager;
    private readonly KeycloakProviderOptions kcOptions;

    public KeycloakUserProvisioningService(
        DatabaseContext db,
        UserManager<AppUser> userManager,
        IOptions<KeycloakProviderOptions> kcOptions)
    {
        this.db = db;
        this.userManager = userManager;
        this.kcOptions = kcOptions.Value;
    }

    public async Task<long?> EnsureUserAsync(AuthScapeIdentity identity, bool autoProvision, CancellationToken ct = default)
    {
        var existing = await db.Users
            .FirstOrDefaultAsync(u =>
                (u.ExternalProvider == identity.ProviderId && u.ExternalSub == identity.ExternalSub)
                || (identity.Email != null && u.Email == identity.Email),
                ct);

        if (existing != null)
        {
            existing.LastLoggedIn = SystemTime.Now;
            if (existing.ExternalProvider == null) existing.ExternalProvider = identity.ProviderId;
            if (existing.ExternalSub == null) existing.ExternalSub = identity.ExternalSub;
            if (string.IsNullOrEmpty(existing.FirstName) && !string.IsNullOrEmpty(identity.GivenName))
                existing.FirstName = identity.GivenName;
            if (string.IsNullOrEmpty(existing.LastName) && !string.IsNullOrEmpty(identity.FamilyName))
                existing.LastName = identity.FamilyName;
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        if (!autoProvision || !kcOptions.AutoProvision)
        {
            return null;
        }

        var user = new AppUser
        {
            UserName         = identity.Email ?? identity.ExternalSub,
            Email            = identity.Email,
            FirstName        = identity.GivenName ?? "",
            LastName         = identity.FamilyName ?? "",
            ExternalProvider = identity.ProviderId,
            ExternalSub      = identity.ExternalSub,
            Created          = SystemTime.Now,
            LastLoggedIn     = SystemTime.Now,
            IsActive         = true,
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(
                $"Failed to provision AppUser for Keycloak sub={identity.ExternalSub}: {errors}");
        }

        return user.Id;
    }
}
