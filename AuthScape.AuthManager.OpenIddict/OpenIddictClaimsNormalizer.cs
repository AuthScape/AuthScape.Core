using Newtonsoft.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthScape.AuthManager.OpenIddict;

/// <summary>
/// Maps the claims that AuthScape's current <c>AuthorizationController</c> attaches to the principal
/// (firstName, lastName, companyId, locationId, usersRoles, userPermissions) into the canonical
/// <see cref="AuthScapeIdentity"/> shape.
/// </summary>
public sealed class OpenIddictClaimsNormalizer : IClaimsNormalizer
{
    /// <inheritdoc />
    public string ProviderId => "openiddict";

    /// <inheritdoc />
    public AuthScapeIdentity Normalize(ExternalIdentity external)
    {
        if (external == null) throw new ArgumentNullException(nameof(external));

        var identity = new AuthScapeIdentity
        {
            ProviderId = ProviderId,
            ExternalSub = external.Sub,
            Email = external.Email,
            EmailVerified = external.EmailVerified,
            DisplayName = external.Name,
            GivenName = external.GivenName ?? GetRawClaim(external, "firstName"),
            FamilyName = external.FamilyName ?? GetRawClaim(external, "lastName"),
            PictureUrl = external.Picture,
        };

        // usersRoles is a JSON array of { Id, Name } objects attached by AuthorizationController.
        // Roles list on AuthScapeIdentity is just the names.
        var usersRolesJson = GetRawClaim(external, "usersRoles");
        if (!string.IsNullOrWhiteSpace(usersRolesJson))
        {
            try
            {
                var roles = JsonConvert.DeserializeObject<List<RoleRef>>(usersRolesJson);
                if (roles != null)
                {
                    foreach (var r in roles)
                    {
                        if (!string.IsNullOrWhiteSpace(r.Name))
                            identity.Roles.Add(r.Name);
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed roles JSON — skip silently rather than break login.
            }
        }

        // Standard role claim is also attached (one per role) — merge any not already present.
        if (external.RawClaims.TryGetValue(Claims.Role, out var singleRole)
            && !string.IsNullOrWhiteSpace(singleRole)
            && !identity.Roles.Contains(singleRole))
        {
            identity.Roles.Add(singleRole);
        }

        // userPermissions is a JSON array of Permission objects. Names go into Permissions list;
        // the full JSON stays in AdditionalClaims for callers that need the full objects.
        var permsJson = GetRawClaim(external, "userPermissions");
        if (!string.IsNullOrWhiteSpace(permsJson))
        {
            identity.AdditionalClaims["userPermissions"] = permsJson;
            try
            {
                var perms = JsonConvert.DeserializeObject<List<PermissionRef>>(permsJson);
                if (perms != null)
                {
                    foreach (var p in perms)
                    {
                        if (!string.IsNullOrWhiteSpace(p.Name))
                            identity.Permissions.Add(p.Name);
                    }
                }
            }
            catch (JsonException) { }
        }

        // Preserve company/location scope on the identity for downstream code that still uses them.
        CopyIfPresent(external, identity, "companyId");
        CopyIfPresent(external, identity, "companyName");
        CopyIfPresent(external, identity, "locationId");
        CopyIfPresent(external, identity, "locationName");

        return identity;
    }

    private static string? GetRawClaim(ExternalIdentity external, string name)
        => external.RawClaims.TryGetValue(name, out var v) ? v : null;

    private static void CopyIfPresent(ExternalIdentity external, AuthScapeIdentity identity, string key)
    {
        if (external.RawClaims.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            identity.AdditionalClaims[key] = value;
    }

    private sealed class RoleRef { public string? Name { get; set; } }
    private sealed class PermissionRef { public string? Name { get; set; } }
}
