using System.Collections.Generic;

namespace AuthScape.Services.Keycloak
{
    /// <summary>
    /// DTOs for Keycloak Admin REST API responses and requests.
    /// Field names mirror Keycloak's representation models so JSON (de)serialization works directly
    /// without custom converters. See https://www.keycloak.org/docs-api/latest/rest-api/index.html.
    /// </summary>
    public class KeycloakClientDto
    {
        public string Id { get; set; }
        public string ClientId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public bool PublicClient { get; set; }
        public bool ServiceAccountsEnabled { get; set; }
        public bool StandardFlowEnabled { get; set; }
        public bool DirectAccessGrantsEnabled { get; set; }
        public List<string> RedirectUris { get; set; } = new();
        public List<string> WebOrigins { get; set; } = new();
        public string Protocol { get; set; }
    }

    public class KeycloakClientCreateDto
    {
        public string ClientId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; } = true;
        public bool PublicClient { get; set; }
        public bool StandardFlowEnabled { get; set; } = true;
        public bool DirectAccessGrantsEnabled { get; set; }
        public List<string> RedirectUris { get; set; } = new();
        public List<string> WebOrigins { get; set; } = new();
    }

    public class KeycloakClientUpdateDto : KeycloakClientCreateDto
    {
        public string Id { get; set; }
    }

    public class KeycloakUserDto
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool Enabled { get; set; }
        public bool EmailVerified { get; set; }
        public long? CreatedTimestamp { get; set; }
    }

    public class KeycloakUserCreateDto
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool Enabled { get; set; } = true;
        public bool EmailVerified { get; set; }
        /// <summary>Optional initial password. If null, admin is expected to send a password-reset action email.</summary>
        public string InitialPassword { get; set; }
        /// <summary>If true, the initial password is marked temporary (user must change on first login).</summary>
        public bool TemporaryPassword { get; set; } = true;
    }

    public class KeycloakUserUpdateDto
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool Enabled { get; set; }
        public bool EmailVerified { get; set; }
    }

    public class KeycloakClientScopeDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Protocol { get; set; }
    }

    public class KeycloakClientScopeCreateDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Protocol { get; set; } = "openid-connect";
    }

    public class KeycloakClientScopeUpdateDto : KeycloakClientScopeCreateDto
    {
        public string Id { get; set; }
    }

    public class KeycloakHealthDto
    {
        public KeycloakHealthStatus Status { get; set; }
        public string Detail { get; set; }
    }

    public enum KeycloakHealthStatus
    {
        Disabled,
        Ok,
        Unreachable,
        Unauthorized,
        Misconfigured
    }
}
