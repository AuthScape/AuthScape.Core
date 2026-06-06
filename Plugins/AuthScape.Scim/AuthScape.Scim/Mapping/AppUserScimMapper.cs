using AuthScape.Models.Users;
using AuthScape.Scim.Models.Dtos;

namespace AuthScape.Scim.Mapping;

public static class AppUserScimMapper
{
    public static ScimUser ToScim(AppUser u, string? locationBasePath = null) => new()
    {
        Id = u.Id.ToString(),
        UserName = u.UserName ?? "",
        ExternalId = u.UserName,
        DisplayName = $"{u.FirstName} {u.LastName}".Trim(),
        Active = !(u.LockoutEnabled && u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow),
        Name = new ScimName
        {
            GivenName = u.FirstName,
            FamilyName = u.LastName,
            Formatted = $"{u.FirstName} {u.LastName}".Trim()
        },
        Emails = string.IsNullOrEmpty(u.Email) ? null : new List<ScimEmail>
        {
            new() { Value = u.Email, Type = "work", Primary = true }
        },
        Meta = new ScimMeta
        {
            ResourceType = "User",
            Created = u.Created.UtcDateTime,
            LastModified = u.Created.UtcDateTime,   // AppUser doesn't have a separate updated column
            Location = locationBasePath != null ? $"{locationBasePath.TrimEnd('/')}/{u.Id}" : null
        }
    };

    public static void Apply(AppUser target, ScimUser source)
    {
        target.UserName = source.UserName;
        target.Email = source.Emails?.FirstOrDefault(e => e.Primary)?.Value
                       ?? source.Emails?.FirstOrDefault()?.Value
                       ?? target.Email;
        target.FirstName = source.Name?.GivenName ?? target.FirstName;
        target.LastName = source.Name?.FamilyName ?? target.LastName;

        if (!source.Active)
        {
            target.LockoutEnabled = true;
            target.LockoutEnd = DateTimeOffset.MaxValue;
        }
        else
        {
            target.LockoutEnabled = false;
            target.LockoutEnd = null;
        }
    }
}
