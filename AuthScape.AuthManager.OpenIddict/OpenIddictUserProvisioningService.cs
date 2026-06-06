using AuthScape.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Context;

namespace AuthScape.AuthManager.OpenIddict;

/// <summary>
/// Resolves an OpenIddict-issued identity to a local <see cref="AppUser"/> row. In Issuing mode
/// the IDP usually creates the AppUser at signup time, so this service mostly just looks up
/// existing rows and updates LastLoggedIn — provisioning runs only as a safety net.
/// </summary>
public sealed class OpenIddictUserProvisioningService : IUserProvisioningService
{
    private readonly DatabaseContext db;
    private readonly UserManager<AppUser> userManager;
    private readonly AuthScapeIdentityOptions options;

    public OpenIddictUserProvisioningService(
        DatabaseContext db,
        UserManager<AppUser> userManager,
        IOptions<AuthScapeIdentityOptions> options)
    {
        this.db = db;
        this.userManager = userManager;
        this.options = options.Value;
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
            existing.LastLoggedIn = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        if (!autoProvision || !options.AutoProvisionUsers)
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
            Created          = DateTimeOffset.UtcNow,
            LastLoggedIn     = DateTimeOffset.UtcNow,
            IsActive         = true,
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(
                $"Failed to provision AppUser for {identity.ProviderId}:{identity.ExternalSub}: {errors}");
        }

        return user.Id;
    }
}
