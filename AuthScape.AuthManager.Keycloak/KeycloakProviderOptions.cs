namespace AuthScape.AuthManager.Keycloak;

/// <summary>
/// Settings for the Keycloak adapter. Mirrors what JwtBearer needs (authority, client id) plus
/// the claim-mapping knobs that Keycloak realms vary on.
/// </summary>
public class KeycloakProviderOptions
{
    /// <summary>Keycloak realm URL (e.g. "https://kc.example.com/realms/myrealm"). Used for OIDC
    /// discovery — JWKS, issuer, end-session URI are all derived from this.</summary>
    public string Authority { get; set; } = "";

    /// <summary>Audience claim AuthScape APIs accept. Typically the client id registered in Keycloak
    /// for the AuthScape backend.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Client secret if Keycloak treats the client as confidential. Optional for public clients.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Require HTTPS for the metadata document. Defaults to true; only set false for dev realms
    /// running on plain HTTP behind a docker network.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Claim name carrying the subject identifier. Standard: "sub".</summary>
    public string SubClaimName { get; set; } = "sub";

    /// <summary>Claim name carrying the email. Standard: "email".</summary>
    public string EmailClaimName { get; set; } = "email";

    /// <summary>Claim name for the verified-email flag. Standard: "email_verified".</summary>
    public string EmailVerifiedClaimName { get; set; } = "email_verified";

    /// <summary>Dotted path to the realm-level roles array. Default matches Keycloak's structure:
    /// realm_access.roles → ["role-a", "role-b"].</summary>
    public string RealmRolesClaimPath { get; set; } = "realm_access.roles";

    /// <summary>Dotted path to the client-level roles array. Optional — Keycloak uses
    /// resource_access.{clientId}.roles. Leave null to skip client roles.</summary>
    public string? ClientRolesClaimPath { get; set; }

    /// <summary>Claim name treated as the user's display name. Keycloak default: "preferred_username".</summary>
    public string NameClaimName { get; set; } = "preferred_username";

    /// <summary>Maps Keycloak role names to AuthScape role names. Keys not in this map pass through
    /// unchanged when <see cref="PassthroughUnmappedRoles"/> is true (default), or are dropped otherwise.</summary>
    public IDictionary<string, string> RoleMapping { get; set; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>If true, role names not present in <see cref="RoleMapping"/> are kept as-is. If false,
    /// only mapped roles are surfaced — useful when AuthScape's role set should be strictly bounded.</summary>
    public bool PassthroughUnmappedRoles { get; set; } = true;

    /// <summary>Auto-create an AuthScapeUser on first validated token. When false, only pre-provisioned
    /// users can sign in.</summary>
    public bool AutoProvision { get; set; } = true;

    /// <summary>
    /// When true, the API validates incoming tokens by calling Keycloak's RFC 7662 introspection
    /// endpoint instead of validating the JWT signature locally. Matches the OpenIddict introspection
    /// pattern used in the AuthScape IDP: every protected request makes a round-trip to Keycloak, so
    /// revoked tokens are rejected immediately at the cost of one HTTP call per request.
    /// Leave false (default) for local JWT validation via JWKS — recommended for high-throughput APIs
    /// with short token lifetimes.
    /// </summary>
    public bool UseIntrospection { get; set; }

    /// <summary>
    /// Client id used when calling the Keycloak introspection endpoint. This is the Keycloak client
    /// representing the API itself (typically the same as <see cref="ClientId"/>). The client must be
    /// confidential and have "Service Accounts Enabled" so it can authenticate to introspect.
    /// Only consulted when <see cref="UseIntrospection"/> is true.
    /// </summary>
    public string? IntrospectionClientId { get; set; }

    /// <summary>
    /// Client secret paired with <see cref="IntrospectionClientId"/>. Only consulted when
    /// <see cref="UseIntrospection"/> is true.
    /// </summary>
    public string? IntrospectionClientSecret { get; set; }
}
