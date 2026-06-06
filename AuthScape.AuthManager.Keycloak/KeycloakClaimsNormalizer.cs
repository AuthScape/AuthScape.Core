using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AuthScape.AuthManager.Keycloak;

/// <summary>
/// Maps Keycloak-issued claims (including its nested realm_access / resource_access JSON structures)
/// into the canonical <see cref="AuthScapeIdentity"/>. Honors the claim-name overrides and role mapping
/// configured on <see cref="KeycloakProviderOptions"/>.
/// </summary>
public sealed class KeycloakClaimsNormalizer : IClaimsNormalizer
{
    private readonly KeycloakProviderOptions options;

    public KeycloakClaimsNormalizer(IOptions<KeycloakProviderOptions> options)
    {
        this.options = options.Value;
    }

    /// <inheritdoc />
    public string ProviderId => "keycloak";

    /// <inheritdoc />
    public AuthScapeIdentity Normalize(ExternalIdentity external)
    {
        if (external == null) throw new ArgumentNullException(nameof(external));

        var sub = Get(external, options.SubClaimName) ?? external.Sub;

        var identity = new AuthScapeIdentity
        {
            ProviderId = ProviderId,
            ExternalSub = sub,
            Email = Get(external, options.EmailClaimName) ?? external.Email,
            EmailVerified = bool.TryParse(Get(external, options.EmailVerifiedClaimName), out var ev)
                ? ev
                : external.EmailVerified,
            DisplayName = Get(external, options.NameClaimName) ?? external.Name,
            GivenName = Get(external, "given_name") ?? external.GivenName,
            FamilyName = Get(external, "family_name") ?? external.FamilyName,
            PictureUrl = Get(external, "picture") ?? external.Picture,
        };

        // Realm roles — Keycloak puts these inside realm_access.roles (a nested JSON array).
        foreach (var role in ExtractRolesByPath(external, options.RealmRolesClaimPath))
        {
            AddRole(identity, role);
        }

        // Optional client roles — resource_access.{clientId}.roles
        if (!string.IsNullOrWhiteSpace(options.ClientRolesClaimPath))
        {
            var path = options.ClientRolesClaimPath.Replace("{clientId}", options.ClientId);
            foreach (var role in ExtractRolesByPath(external, path))
            {
                AddRole(identity, role);
            }
        }

        // Stash anything we didn't translate so callers can reach raw provider claims if needed.
        foreach (var kv in external.RawClaims)
        {
            if (!identity.AdditionalClaims.ContainsKey(kv.Key))
                identity.AdditionalClaims[kv.Key] = kv.Value;
        }

        return identity;
    }

    private void AddRole(AuthScapeIdentity identity, string keycloakRole)
    {
        if (string.IsNullOrWhiteSpace(keycloakRole)) return;

        if (options.RoleMapping.TryGetValue(keycloakRole, out var mapped))
        {
            if (!identity.Roles.Contains(mapped))
                identity.Roles.Add(mapped);
            return;
        }

        if (options.PassthroughUnmappedRoles && !identity.Roles.Contains(keycloakRole))
            identity.Roles.Add(keycloakRole);
    }

    private static string? Get(ExternalIdentity external, string claimName)
        => external.RawClaims.TryGetValue(claimName, out var v) ? v : null;

    /// <summary>
    /// Walks a dotted path (e.g. "realm_access.roles") through the raw claims dictionary, which may
    /// contain JSON-string blobs at intermediate keys, and returns the leaf array as strings.
    /// </summary>
    private static IEnumerable<string> ExtractRolesByPath(ExternalIdentity external, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) yield break;

        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) yield break;

        // The first segment must exist as a top-level raw claim. JwtBearer stores nested JSON as a
        // JSON-string under the parent claim name, which we re-parse here.
        if (!external.RawClaims.TryGetValue(parts[0], out var json) || string.IsNullOrWhiteSpace(json))
            yield break;

        JsonElement element;
        try
        {
            using var doc = JsonDocument.Parse(json);
            element = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            yield break;
        }

        for (int i = 1; i < parts.Length; i++)
        {
            if (element.ValueKind != JsonValueKind.Object) yield break;
            if (!element.TryGetProperty(parts[i], out var next)) yield break;
            element = next;
        }

        if (element.ValueKind != JsonValueKind.Array) yield break;

        foreach (var role in element.EnumerateArray())
        {
            if (role.ValueKind == JsonValueKind.String)
            {
                var s = role.GetString();
                if (!string.IsNullOrWhiteSpace(s)) yield return s;
            }
        }
    }
}
