using System.Collections.Generic;

namespace IDP.Models.IdentityServer
{
    public class ApplicationCreateDto
    {
        public string ClientId { get; set; }
        public ClientType ClientType { get; set; }
        public string DisplayName { get; set; }
        public string? Description { get; set; }
        public List<string> RedirectUris { get; set; } = new List<string>();
        public List<string> PostLogoutRedirectUris { get; set; } = new List<string>();
        public List<string> AllowedOrigins { get; set; } = new List<string>();
        public List<string> AllowedScopes { get; set; } = new List<string>();

        // Optional overrides
        public string? ClientSecret { get; set; }
        public bool? RequirePkce { get; set; }
        public bool? AllowOfflineAccess { get; set; }

        // Token lifetimes
        public int? AccessTokenLifetime { get; set; }
        public int? IdentityTokenLifetime { get; set; }
        public int? RefreshTokenLifetime { get; set; }
    }
}
