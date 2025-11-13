namespace Authscape.IdentityServer.Models
{
    public class ApplicationDetailsDto
    {
        public string Id { get; set; }
        public string ClientId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public ClientType ClientType { get; set; }
        public string ClientSecret { get; set; } // Masked except on creation
        public List<string> RedirectUris { get; set; } = new List<string>();
        public List<string> PostLogoutRedirectUris { get; set; } = new List<string>();
        public List<string> AllowedOrigins { get; set; } = new List<string>();
        public List<string> Permissions { get; set; } = new List<string>();
        public List<string> Requirements { get; set; } = new List<string>();
        public List<string> AllowedScopes { get; set; } = new List<string>();

        // Token settings
        public int? AccessTokenLifetime { get; set; }
        public int? IdentityTokenLifetime { get; set; }
        public int? RefreshTokenLifetime { get; set; }
        public bool AllowOfflineAccess { get; set; }
        public bool RequirePkce { get; set; }
        public bool RequireClientSecret { get; set; }

        // Metadata
        public DateTime? CreatedDate { get; set; }
        public string Status { get; set; } // Active/Inactive
    }
}
