using AuthScape.Models.Authentication;
using IDP.Models.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Services.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace IDP.Services.IdentityServer
{
    public interface ISSOProviderService
    {
        Task<List<SSOProviderDto>> GetAllProvidersAsync();
        Task<SSOProviderDto> GetProviderAsync(ThirdPartyAuthenticationType providerType);
        Task SaveProviderConfigurationAsync(SSOProviderConfigDto dto);
        Task ToggleProviderAsync(ThirdPartyAuthenticationType providerType, bool enabled);
        Task DeleteProviderConfigurationAsync(ThirdPartyAuthenticationType providerType);
    }

    public class SSOProviderService : ISSOProviderService
    {
        private readonly DatabaseContext dbContext;

        public SSOProviderService(DatabaseContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<List<SSOProviderDto>> GetAllProvidersAsync()
        {
            var allProviders = Enum.GetValues<ThirdPartyAuthenticationType>()
                .Where(p => p != ThirdPartyAuthenticationType.Custom)
                .ToList();

            var configured = await dbContext.ThirdPartyAuthentications.ToListAsync();

            var providers = new List<SSOProviderDto>();

            foreach (var providerType in allProviders)
            {
                var config = configured.FirstOrDefault(c => c.ThirdPartyAuthenticationType == providerType);
                var metadata = GetProviderMetadata(providerType);

                providers.Add(new SSOProviderDto
                {
                    ProviderType = providerType,
                    ProviderName = providerType.ToString(),
                    IsEnabled = config?.IsEnabled ?? false,
                    IsConfigured = config != null && !string.IsNullOrEmpty(config.ClientId),
                    ClientId = config?.ClientId,
                    ClientSecret = config?.ClientSecret,
                    RedirectUri = config?.RedirectUri,
                    Scopes = config?.Scopes,
                    AdditionalSettings = config?.AdditionalSettings,
                    DisplayOrder = config?.DisplayOrder ?? (int)providerType,
                    CreatedAt = config?.CreatedAt,
                    UpdatedAt = config?.UpdatedAt,
                    DisplayName = metadata.DisplayName,
                    Description = metadata.Description,
                    Icon = metadata.Icon,
                    BrandColor = metadata.BrandColor,
                    RequiredFields = metadata.RequiredFields
                });
            }

            return providers.OrderBy(p => p.DisplayOrder).ToList();
        }

        public async Task<SSOProviderDto> GetProviderAsync(ThirdPartyAuthenticationType providerType)
        {
            var config = await dbContext.ThirdPartyAuthentications
                .FirstOrDefaultAsync(p => p.ThirdPartyAuthenticationType == providerType);

            var metadata = GetProviderMetadata(providerType);

            return new SSOProviderDto
            {
                ProviderType = providerType,
                ProviderName = providerType.ToString(),
                IsEnabled = config?.IsEnabled ?? false,
                IsConfigured = config != null,
                ClientId = config?.ClientId,
                ClientSecret = config?.ClientSecret,
                RedirectUri = config?.RedirectUri,
                Scopes = config?.Scopes,
                AdditionalSettings = config?.AdditionalSettings,
                DisplayOrder = config?.DisplayOrder ?? (int)providerType,
                CreatedAt = config?.CreatedAt,
                UpdatedAt = config?.UpdatedAt,
                DisplayName = metadata.DisplayName,
                Description = metadata.Description,
                Icon = metadata.Icon,
                BrandColor = metadata.BrandColor,
                RequiredFields = metadata.RequiredFields
            };
        }

        public async Task SaveProviderConfigurationAsync(SSOProviderConfigDto dto)
        {
            var existing = await dbContext.ThirdPartyAuthentications
                .FirstOrDefaultAsync(p => p.ThirdPartyAuthenticationType == dto.ProviderType);

            // Build additional settings JSON for provider-specific fields
            var additionalSettings = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(dto.TenantId)) additionalSettings["TenantId"] = dto.TenantId;
            if (!string.IsNullOrEmpty(dto.TeamId)) additionalSettings["TeamId"] = dto.TeamId;
            if (!string.IsNullOrEmpty(dto.KeyId)) additionalSettings["KeyId"] = dto.KeyId;
            if (!string.IsNullOrEmpty(dto.PrivateKey)) additionalSettings["PrivateKey"] = dto.PrivateKey;
            if (!string.IsNullOrEmpty(dto.Region)) additionalSettings["Region"] = dto.Region;
            if (!string.IsNullOrEmpty(dto.EnterpriseDomain)) additionalSettings["EnterpriseDomain"] = dto.EnterpriseDomain;

            if (existing == null)
            {
                var metadata = GetProviderMetadata(dto.ProviderType);
                existing = new ThirdPartyAuthentication
                {
                    ThirdPartyAuthenticationType = dto.ProviderType,
                    ProviderName = metadata.DisplayName,
                    IsEnabled = dto.IsEnabled,
                    ClientId = dto.ClientId,
                    ClientSecret = dto.ClientSecret,
                    Scopes = dto.Scopes,
                    AdditionalSettings = additionalSettings.Count > 0 ? JsonSerializer.Serialize(additionalSettings) : null,
                    DisplayOrder = (int)dto.ProviderType,
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.ThirdPartyAuthentications.Add(existing);
            }
            else
            {
                existing.IsEnabled = dto.IsEnabled;
                existing.ClientId = dto.ClientId;
                existing.ClientSecret = dto.ClientSecret;
                existing.Scopes = dto.Scopes;
                existing.AdditionalSettings = additionalSettings.Count > 0 ? JsonSerializer.Serialize(additionalSettings) : null;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync();
        }

        public async Task ToggleProviderAsync(ThirdPartyAuthenticationType providerType, bool enabled)
        {
            var existing = await dbContext.ThirdPartyAuthentications
                .FirstOrDefaultAsync(p => p.ThirdPartyAuthenticationType == providerType);

            if (existing != null)
            {
                existing.IsEnabled = enabled;
                existing.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task DeleteProviderConfigurationAsync(ThirdPartyAuthenticationType providerType)
        {
            var existing = await dbContext.ThirdPartyAuthentications
                .FirstOrDefaultAsync(p => p.ThirdPartyAuthenticationType == providerType);

            if (existing != null)
            {
                dbContext.ThirdPartyAuthentications.Remove(existing);
                await dbContext.SaveChangesAsync();
            }
        }

        private (string DisplayName, string Description, string Icon, string BrandColor, List<SSOProviderField> RequiredFields) GetProviderMetadata(ThirdPartyAuthenticationType providerType)
        {
            return providerType switch
            {
                ThirdPartyAuthenticationType.Facebook => (
                    "Facebook",
                    "Allow users to sign in with their Facebook account",
                    "fa-facebook",
                    "#1877F2",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "App ID", Type = "text", Required = true, Placeholder = "Enter Facebook App ID", HelpText = "Found in Facebook Developer Console" },
                        new() { Name = "ClientSecret", Label = "App Secret", Type = "password", Required = true, Placeholder = "Enter App Secret", HelpText = "Found in Facebook Developer Console" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "email,public_profile", HelpText = "Comma-separated list of permissions" }
                    }
                ),
                ThirdPartyAuthenticationType.Google => (
                    "Google",
                    "Allow users to sign in with their Google account",
                    "fa-google",
                    "#4285F4",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Google Client ID", HelpText = "Found in Google Cloud Console" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Google Cloud Console" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "openid,profile,email", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Microsoft => (
                    "Microsoft",
                    "Allow users to sign in with their Microsoft account",
                    "fa-windows",
                    "#00A4EF",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Application (client) ID", Type = "text", Required = true, Placeholder = "Enter Client ID", HelpText = "Found in Azure Portal" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Created in Azure Portal" },
                        new() { Name = "TenantId", Label = "Tenant ID", Type = "text", Required = false, Placeholder = "common", HelpText = "Optional - use 'common' for multi-tenant" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "User.Read", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Apple => (
                    "Apple",
                    "Allow users to sign in with their Apple ID",
                    "fa-apple",
                    "#000000",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Services ID", Type = "text", Required = true, Placeholder = "com.yourcompany.app", HelpText = "Your Services ID" },
                        new() { Name = "TeamId", Label = "Team ID", Type = "text", Required = true, Placeholder = "Enter Team ID", HelpText = "Found in Apple Developer Portal" },
                        new() { Name = "KeyId", Label = "Key ID", Type = "text", Required = true, Placeholder = "Enter Key ID", HelpText = "Private Key ID" },
                        new() { Name = "PrivateKey", Label = "Private Key", Type = "textarea", Required = true, Placeholder = "-----BEGIN PRIVATE KEY-----", HelpText = "Your .p8 private key content" }
                    }
                ),
                ThirdPartyAuthenticationType.Github => (
                    "GitHub",
                    "Allow users to sign in with their GitHub account",
                    "fa-github",
                    "#181717",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter GitHub Client ID", HelpText = "Found in GitHub OAuth App settings" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Generated in GitHub OAuth App settings" },
                        new() { Name = "EnterpriseDomain", Label = "Enterprise Domain", Type = "text", Required = false, Placeholder = "github.company.com", HelpText = "For GitHub Enterprise only" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "user:email", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Twitter => (
                    "Twitter",
                    "Allow users to sign in with their Twitter account",
                    "fa-twitter",
                    "#1DA1F2",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "API Key", Type = "text", Required = true, Placeholder = "Enter Twitter API Key", HelpText = "Found in Twitter Developer Portal" },
                        new() { Name = "ClientSecret", Label = "API Secret Key", Type = "password", Required = true, Placeholder = "Enter API Secret", HelpText = "Found in Twitter Developer Portal" }
                    }
                ),
                ThirdPartyAuthenticationType.Discord => (
                    "Discord",
                    "Allow users to sign in with their Discord account",
                    "fa-comments",
                    "#5865F2",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Discord Client ID", HelpText = "Found in Discord Developer Portal" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Discord Developer Portal" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "identify,email", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Spotify => (
                    "Spotify",
                    "Allow users to sign in with their Spotify account",
                    "fa-spotify",
                    "#1DB954",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Spotify Client ID", HelpText = "Found in Spotify Dashboard" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Spotify Dashboard" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "user-read-email", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Amazon => (
                    "Amazon",
                    "Allow users to sign in with their Amazon account",
                    "fa-amazon",
                    "#FF9900",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Amazon Client ID", HelpText = "Found in Amazon Developer Console" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Amazon Developer Console" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "profile", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Slack => (
                    "Slack",
                    "Allow users to sign in with their Slack account",
                    "fa-slack",
                    "#4A154B",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Slack Client ID", HelpText = "Found in Slack App settings" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Slack App settings" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "identity.basic,identity.email", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.LinkedIn => (
                    "LinkedIn",
                    "Allow users to sign in with their LinkedIn account",
                    "fa-linkedin",
                    "#0A66C2",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter LinkedIn Client ID", HelpText = "Found in LinkedIn Developer Portal" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in LinkedIn Developer Portal" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "r_emailaddress,r_liteprofile", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.HubSpot => (
                    "HubSpot",
                    "Allow users to sign in with their HubSpot account",
                    "fa-bullhorn",
                    "#FF7A59",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter HubSpot Client ID", HelpText = "Found in HubSpot App settings" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in HubSpot App settings" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "oauth", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Fitbit => (
                    "Fitbit",
                    "Allow users to sign in with their Fitbit account",
                    "fa-heartbeat",
                    "#00B0B9",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Fitbit Client ID", HelpText = "Found in Fitbit Developer Portal" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Fitbit Developer Portal" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "profile", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Coinbase => (
                    "Coinbase",
                    "Allow users to sign in with their Coinbase account",
                    "fa-bitcoin",
                    "#0052FF",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Coinbase Client ID", HelpText = "Found in Coinbase API settings" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Coinbase API settings" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "wallet:user:read", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.BattleNet => (
                    "Battle.net",
                    "Allow users to sign in with their Battle.net account",
                    "fa-gamepad",
                    "#148EFF",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Battle.net Client ID", HelpText = "Found in Battle.net Developer Portal" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Battle.net Developer Portal" },
                        new() { Name = "Region", Label = "Region", Type = "text", Required = false, Placeholder = "us", HelpText = "Region: us, eu, kr, cn, sea" }
                    }
                ),
                ThirdPartyAuthenticationType.Basecamp => (
                    "Basecamp",
                    "Allow users to sign in with their Basecamp account",
                    "fa-briefcase",
                    "#1D2D35",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Basecamp Client ID", HelpText = "Found in Basecamp OAuth settings" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Basecamp OAuth settings" }
                    }
                ),
                ThirdPartyAuthenticationType.Autodesk => (
                    "Autodesk",
                    "Allow users to sign in with their Autodesk account",
                    "fa-cube",
                    "#0696D7",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Autodesk Client ID", HelpText = "Found in Autodesk Forge Portal" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Autodesk Forge Portal" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "user-profile:read", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Asana => (
                    "Asana",
                    "Allow users to sign in with their Asana account",
                    "fa-tasks",
                    "#F06A6A",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Asana Client ID", HelpText = "Found in Asana Developer Console" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Asana Developer Console" }
                    }
                ),
                ThirdPartyAuthenticationType.AdobeIO => (
                    "Adobe",
                    "Allow users to sign in with their Adobe account",
                    "fa-file-pdf-o",
                    "#FF0000",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Adobe Client ID", HelpText = "Found in Adobe Developer Console" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Adobe Developer Console" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "openid,email,profile", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Dropbox => (
                    "Dropbox",
                    "Allow users to sign in with their Dropbox account",
                    "fa-dropbox",
                    "#0061FF",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "App Key", Type = "text", Required = true, Placeholder = "Enter Dropbox App Key", HelpText = "Found in Dropbox App Console" },
                        new() { Name = "ClientSecret", Label = "App Secret", Type = "password", Required = true, Placeholder = "Enter App Secret", HelpText = "Found in Dropbox App Console" }
                    }
                ),
                ThirdPartyAuthenticationType.Notion => (
                    "Notion",
                    "Allow users to sign in with their Notion account",
                    "fa-sticky-note-o",
                    "#000000",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "OAuth Client ID", Type = "text", Required = true, Placeholder = "Enter Notion Client ID", HelpText = "Found in Notion Integrations" },
                        new() { Name = "ClientSecret", Label = "OAuth Secret", Type = "password", Required = true, Placeholder = "Enter OAuth Secret", HelpText = "Found in Notion Integrations" }
                    }
                ),
                ThirdPartyAuthenticationType.Patreon => (
                    "Patreon",
                    "Allow users to sign in with their Patreon account",
                    "fa-credit-card",
                    "#FF424D",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Patreon Client ID", HelpText = "Found in Patreon Client Portal" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Patreon Client Portal" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "identity,identity[email]", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.Paypal => (
                    "PayPal",
                    "Allow users to sign in with their PayPal account",
                    "fa-paypal",
                    "#00457C",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter PayPal Client ID", HelpText = "Found in PayPal Developer Dashboard" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in PayPal Developer Dashboard" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "openid,profile,email", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                ThirdPartyAuthenticationType.WordPress => (
                    "WordPress",
                    "Allow users to sign in with their WordPress.com account",
                    "fa-wordpress",
                    "#21759B",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter WordPress Client ID", HelpText = "Found in WordPress.com Applications" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in WordPress.com Applications" }
                    }
                ),
                ThirdPartyAuthenticationType.Yammer => (
                    "Yammer",
                    "Allow users to sign in with their Yammer account",
                    "fa-users",
                    "#0072C6",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Yammer Client ID", HelpText = "Found in Yammer App settings" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Yammer App settings" }
                    }
                ),
                ThirdPartyAuthenticationType.Yahoo => (
                    "Yahoo",
                    "Allow users to sign in with their Yahoo account",
                    "fa-yahoo",
                    "#6001D2",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Yahoo Client ID", HelpText = "Found in Yahoo Developer Network" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Yahoo Developer Network" }
                    }
                ),
                ThirdPartyAuthenticationType.MailChimp => (
                    "MailChimp",
                    "Allow users to sign in with their MailChimp account",
                    "fa-envelope",
                    "#FFE01B",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter MailChimp Client ID", HelpText = "Found in MailChimp App settings" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in MailChimp App settings" }
                    }
                ),
                ThirdPartyAuthenticationType.Twitch => (
                    "Twitch",
                    "Allow users to sign in with their Twitch account",
                    "fa-twitch",
                    "#9146FF",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Twitch Client ID", HelpText = "Found in Twitch Developer Console" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "Found in Twitch Developer Console" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "user:read:email", HelpText = "Comma-separated list of scopes" }
                    }
                ),
                // Fallback for any custom providers
                _ => (
                    providerType.ToString(),
                    $"Allow users to sign in with their {providerType} account",
                    "fa-sign-in",
                    "#667eea",
                    new List<SSOProviderField>
                    {
                        new() { Name = "ClientId", Label = "Client ID", Type = "text", Required = true, Placeholder = "Enter Client ID", HelpText = "" },
                        new() { Name = "ClientSecret", Label = "Client Secret", Type = "password", Required = true, Placeholder = "Enter Client Secret", HelpText = "" },
                        new() { Name = "Scopes", Label = "Scopes", Type = "text", Required = false, Placeholder = "", HelpText = "Comma-separated list of scopes" }
                    }
                )
            };
        }
    }
}
