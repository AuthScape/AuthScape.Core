using AuthScape.Models.Authentication;
using System;
using System.Collections.Generic;

namespace IDP.Models.IdentityServer
{
    public class SSOProviderDto
    {
        public ThirdPartyAuthenticationType ProviderType { get; set; }
        public string ProviderName { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsConfigured { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUri { get; set; }
        public string Scopes { get; set; }
        public string AdditionalSettings { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Metadata (not stored in DB)
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; } // Font Awesome icon class
        public string BrandColor { get; set; } // Brand color for icon background
        public List<SSOProviderField> RequiredFields { get; set; } = new();
    }

    public class SSOProviderField
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string Type { get; set; } // text, password, textarea
        public bool Required { get; set; }
        public string Placeholder { get; set; }
        public string HelpText { get; set; }
    }

    public class SSOProviderConfigDto
    {
        public ThirdPartyAuthenticationType ProviderType { get; set; }
        public bool IsEnabled { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Scopes { get; set; }

        // Provider-specific settings
        public string TenantId { get; set; } // Microsoft
        public string TeamId { get; set; } // Apple
        public string KeyId { get; set; } // Apple
        public string PrivateKey { get; set; } // Apple
        public string Region { get; set; } // Battle.Net
        public string EnterpriseDomain { get; set; } // GitHub
    }
}
