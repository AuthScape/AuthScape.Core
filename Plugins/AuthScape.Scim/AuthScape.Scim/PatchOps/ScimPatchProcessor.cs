using AuthScape.Models.Users;
using AuthScape.Scim.Models.Dtos;
using System.Text.Json;

namespace AuthScape.Scim.PatchOps;

/// <summary>
/// Applies SCIM 2.0 PATCH operations to an AppUser. Supports the operation shapes that real-world
/// IdPs (Okta, Azure AD, OneLogin) emit:
///
/// 1. Whole-resource replace: { "op": "replace", "value": { "active": false, ... } }
/// 2. Path-targeted replace:  { "op": "replace", "path": "userName", "value": "new" }
/// 3. Path-targeted add:      { "op": "add", "path": "emails", "value": [{...}] }
/// 4. Path-targeted remove:   { "op": "remove", "path": "userName" }
///
/// Filtered paths like emails[type eq "work"].value are NOT supported; most IdPs don't emit them
/// because Azure AD/Okta typically replace the whole emails array. Add filtered-path support if
/// you onboard an IdP that requires it.
/// </summary>
public static class ScimPatchProcessor
{
    public static void Apply(AppUser user, ScimPatchRequest patch)
    {
        if (patch?.Operations == null) return;

        foreach (var op in patch.Operations)
        {
            switch (op.Op?.ToLowerInvariant())
            {
                case "add":
                case "replace":
                    ApplyAddOrReplace(user, op);
                    break;
                case "remove":
                    ApplyRemove(user, op);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported PATCH op: {op.Op}");
            }
        }
    }

    private static void ApplyAddOrReplace(AppUser user, ScimPatchOperation op)
    {
        if (string.IsNullOrEmpty(op.Path))
        {
            // Whole-resource update: value is a partial ScimUser.
            if (op.Value is not JsonElement el || el.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("PATCH without path requires an object value");

            foreach (var prop in el.EnumerateObject())
                ApplyAttribute(user, prop.Name, prop.Value);
            return;
        }

        if (!op.Value.HasValue)
            throw new InvalidOperationException($"PATCH op '{op.Op}' requires a value");

        ApplyAttribute(user, op.Path, op.Value.Value);
    }

    private static void ApplyRemove(AppUser user, ScimPatchOperation op)
    {
        if (string.IsNullOrEmpty(op.Path))
            throw new InvalidOperationException("PATCH 'remove' requires a path");

        ApplyAttribute(user, op.Path, defaultRemove: true);
    }

    private static void ApplyAttribute(AppUser user, string path, JsonElement value)
    {
        switch (path.ToLowerInvariant())
        {
            case "username":
                user.UserName = value.GetString();
                break;
            case "displayname":
                // No direct AppUser column; ignore (SCIM allows the server to drop unknown attributes silently).
                break;
            case "name.givenname":
                user.FirstName = value.GetString() ?? "";
                break;
            case "name.familyname":
                user.LastName = value.GetString() ?? "";
                break;
            case "active":
                ApplyActive(user, value.ValueKind == JsonValueKind.True);
                break;
            case "emails":
                if (value.ValueKind == JsonValueKind.Array)
                {
                    var primary = ExtractPrimaryEmail(value);
                    if (primary != null) user.Email = primary;
                }
                else if (value.ValueKind == JsonValueKind.String)
                {
                    user.Email = value.GetString();
                }
                break;
            default:
                // Unknown path — silently drop per SCIM tolerance principle.
                // For tenant-specific extensions, look up ScimConfiguration.AttributeMappingsJson here.
                break;
        }
    }

    private static void ApplyAttribute(AppUser user, string path, bool defaultRemove)
    {
        switch (path.ToLowerInvariant())
        {
            case "username": user.UserName = null; break;
            case "name.givenname": user.FirstName = ""; break;
            case "name.familyname": user.LastName = ""; break;
            case "emails": user.Email = null; break;
            case "active":
                ApplyActive(user, false);
                break;
        }
    }

    private static void ApplyActive(AppUser user, bool active)
    {
        user.LockoutEnabled = !active;
        user.LockoutEnd = active ? null : DateTimeOffset.MaxValue;
    }

    private static string? ExtractPrimaryEmail(JsonElement emailsArray)
    {
        string? first = null;
        foreach (var item in emailsArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var value = item.TryGetProperty("value", out var v) ? v.GetString() : null;
            if (value == null) continue;
            first ??= value;
            if (item.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.True)
                return value;
        }
        return first;
    }
}
