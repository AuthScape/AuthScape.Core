using AuthScape.Models.Users;
using AuthScape.Scim.Filters;
using AuthScape.Scim.Mapping;
using AuthScape.Scim.Models.Dtos;
using AuthScape.Scim.PatchOps;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Context;

namespace AuthScape.Scim;

public class ScimService : IScimService
{
    private readonly DatabaseContext db;
    private readonly UserManager<AppUser> userManager;
    private readonly ILogger<ScimService> logger;

    public ScimService(
        DatabaseContext db,
        UserManager<AppUser> userManager,
        ILogger<ScimService> logger)
    {
        this.db = db;
        this.userManager = userManager;
        this.logger = logger;
    }

    public async Task<ScimListResponse<ScimUser>> ListUsersAsync(
        long companyId, ScimQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<AppUser> q = db.Users.Where(u => u.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            var predicate = ScimFilterParser.Parse(query.Filter);
            if (predicate != null) q = q.Where(predicate);
        }

        var total = await q.CountAsync(cancellationToken);

        var startIndex = Math.Max(query.StartIndex, 1);
        var count = Math.Clamp(query.Count, 0, 200);
        var page = await q
            .OrderBy(u => u.Id)
            .Skip(startIndex - 1)
            .Take(count)
            .ToListAsync(cancellationToken);

        return new ScimListResponse<ScimUser>
        {
            TotalResults = total,
            StartIndex = startIndex,
            ItemsPerPage = page.Count,
            Resources = page.Select(u => AppUserScimMapper.ToScim(u)).ToList()
        };
    }

    public async Task<ScimUser?> GetUserAsync(long companyId, string id, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(id, out var userId)) return null;
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);
        return user == null ? null : AppUserScimMapper.ToScim(user);
    }

    public async Task<ScimUser> CreateUserAsync(long companyId, ScimUser scim, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scim.UserName))
            throw new InvalidOperationException("userName is required");

        var existing = await db.Users
            .FirstOrDefaultAsync(u => u.UserName == scim.UserName && u.CompanyId == companyId, cancellationToken);
        if (existing != null)
            throw new ScimConflictException($"A user with userName '{scim.UserName}' already exists in this tenant.");

        var user = new AppUser
        {
            UserName = scim.UserName,
            Email = scim.Emails?.FirstOrDefault(e => e.Primary)?.Value ?? scim.Emails?.FirstOrDefault()?.Value,
            FirstName = scim.Name?.GivenName ?? "",
            LastName = scim.Name?.FamilyName ?? "",
            Created = DateTimeOffset.UtcNow,
            CompanyId = companyId,
            EmailConfirmed = true,   // SCIM-provisioned users are trusted
            LockoutEnabled = !scim.Active,
            LockoutEnd = scim.Active ? null : DateTimeOffset.MaxValue
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to create user: " + string.Join("; ", result.Errors.Select(e => e.Description)));

        return AppUserScimMapper.ToScim(user);
    }

    public async Task<ScimUser> ReplaceUserAsync(long companyId, string id, ScimUser scim, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(id, out var userId))
            throw new ScimNotFoundException();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken)
                   ?? throw new ScimNotFoundException();

        AppUserScimMapper.Apply(user, scim);
        await db.SaveChangesAsync(cancellationToken);
        return AppUserScimMapper.ToScim(user);
    }

    public async Task<ScimUser> PatchUserAsync(long companyId, string id, ScimPatchRequest patch, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(id, out var userId))
            throw new ScimNotFoundException();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken)
                   ?? throw new ScimNotFoundException();

        ScimPatchProcessor.Apply(user, patch);
        await db.SaveChangesAsync(cancellationToken);
        return AppUserScimMapper.ToScim(user);
    }

    public async Task<bool> DeleteUserAsync(long companyId, string id, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(id, out var userId)) return false;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);
        if (user == null) return false;

        // Soft delete: deactivate rather than hard-delete, since AppUser has FKs to many other tables.
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class ScimNotFoundException : Exception
{
    public ScimNotFoundException() : base("Resource not found") { }
}

public class ScimConflictException : Exception
{
    public ScimConflictException(string message) : base(message) { }
}
