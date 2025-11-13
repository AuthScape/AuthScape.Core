namespace Authscape.IdentityServer.Models
{
    public class ApplicationUpdateDto
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string? ClientSecret { get; set; } // Optional - only set when regenerating
        public List<string> RedirectUris { get; set; } = new List<string>();
        public List<string> PostLogoutRedirectUris { get; set; } = new List<string>();
        public List<string> AllowedOrigins { get; set; } = new List<string>();
        public List<string> AllowedScopes { get; set; } = new List<string>();
        public bool? RequirePkce { get; set; }
        public bool AllowOfflineAccess { get; set; }
        public int? AccessTokenLifetime { get; set; }
        public int? IdentityTokenLifetime { get; set; }
        public int? RefreshTokenLifetime { get; set; }
    }
}
