namespace Authscape.IdentityServer.Models
{
    public class ApplicationCreateDto
    {
        public string ClientId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public ClientType ClientType { get; set; }
        public string ClientSecret { get; set; } // Optional - will be generated if not provided
        public List<string> RedirectUris { get; set; } = new List<string>();
        public List<string> PostLogoutRedirectUris { get; set; } = new List<string>();
        public List<string> AllowedOrigins { get; set; } = new List<string>();
        public List<string> AllowedScopes { get; set; } = new List<string>();

        // Optional overrides (defaults based on ClientType)
        public bool? RequirePkce { get; set; }
        public bool? AllowOfflineAccess { get; set; }
        public int? AccessTokenLifetime { get; set; }
        public int? IdentityTokenLifetime { get; set; }
        public int? RefreshTokenLifetime { get; set; }
    }
}
