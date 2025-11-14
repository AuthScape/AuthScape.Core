using IDP.Models.IdentityServer;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Services.Context;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static OpenIddict.Abstractions.OpenIddictConstants;
using PasswordGenerator = AuthScape.Backpack.PasswordGenerator;
using Microsoft.AspNetCore.Identity;

namespace IDP.Services.IdentityServer
{
    public interface IIdentityServerService
    {
        Task<List<OpenIddictEntityFrameworkCoreApplication>> GetApplications();
        Task SetupDevelopmentEnvironment();
        Task<OpenIddictEntityFrameworkCoreApplication> GetApplication(string applicationId);

        // Admin methods
        Task<List<ApplicationDetailsDto>> GetAllApplicationsAsync();
        Task<List<ApplicationDetailsDto>> GetResourceServerApplicationsAsync();
        Task<ApplicationDetailsDto> GetApplicationDetailsAsync(string id);
        Task<(string ClientSecret, string ApplicationId)> CreateApplicationAsync(ApplicationCreateDto dto);
        Task UpdateApplicationAsync(ApplicationUpdateDto dto);
        Task DeleteApplicationAsync(string id);

        Task<List<ScopeDto>> GetAllScopesAsync();
        Task<ScopeDto> GetScopeAsync(string id);
        Task CreateScopeAsync(ScopeCreateDto dto);
        Task UpdateScopeAsync(ScopeUpdateDto dto);
        Task DeleteScopeAsync(string id);
    }

    public class IdentityServerService : IIdentityServerService
    {
        readonly DatabaseContext databaseContext;
        readonly IOpenIddictApplicationManager openIddictApplicationManager;
        readonly IOpenIddictScopeManager openIddictScopeManager;
        public IdentityServerService(DatabaseContext databaseContext, IOpenIddictApplicationManager openIddictApplicationManager, IOpenIddictScopeManager openIddictScopeManager)
        {
            this.databaseContext = databaseContext;
            this.openIddictApplicationManager = openIddictApplicationManager;
            this.openIddictScopeManager = openIddictScopeManager;
        }

        public async Task<List<OpenIddictEntityFrameworkCoreApplication>> GetApplications()
        {
            return await databaseContext.OpenIddictApplications.ToListAsync();
        }

        public async Task<OpenIddictEntityFrameworkCoreApplication> GetApplication(string applicationId)
        {
            return await databaseContext.OpenIddictApplications.Where(a => a.Id == applicationId).FirstOrDefaultAsync();
        }

        public async Task SetupDevelopmentEnvironment()
        {
            await CreateDevelopmentApplicationsAsync();
            await CreateDevelopmentScopesAsync();
        }

        private async Task CreateDevelopmentApplicationsAsync()
        {
            if (await openIddictApplicationManager.FindByClientIdAsync("postman") is null)
            {
                await openIddictApplicationManager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "postman",
                    ClientSecret = "postman-secret",
                    DisplayName = "Postman",
                    RedirectUris = {
                            new Uri("http://localhost:3000/signin-oidc")
                        },
                    PostLogoutRedirectUris =
                        {
                            new Uri("http://localhost:3000/signout-oidc")
                        },
                    Permissions =
                        {
                            Permissions.Endpoints.Authorization,
                            OpenIddictConstants.Permissions.Endpoints.EndSession,
                            Permissions.Endpoints.Token,
                            Permissions.GrantTypes.AuthorizationCode,
                            Permissions.GrantTypes.RefreshToken,
                            Permissions.ResponseTypes.Code,
                            Permissions.Scopes.Email,
                            Permissions.Scopes.Profile,
                            Permissions.Scopes.Roles,
                            Permissions.Prefixes.Scope + "api1",
                        },
                    Requirements =
                        {
                            Requirements.Features.ProofKeyForCodeExchange
                        }
                });
            }


            if (await openIddictApplicationManager.FindByClientIdAsync("resource_server_1") == null)
            {
                var descriptor = new OpenIddictApplicationDescriptor
                {
                    ClientId = "resource_server_1",
                    ClientSecret = "846B62D0-DEF9-4215-A99D-86E6B8DAB342",
                    Permissions =
                        {
                            Permissions.Endpoints.Introspection
                        }
                };

                await openIddictApplicationManager.CreateAsync(descriptor);
            }
        }

        private async Task CreateDevelopmentScopesAsync()
        {
            if (await openIddictScopeManager.FindByNameAsync("api1") == null)
            {
                var descriptor = new OpenIddictScopeDescriptor
                {
                    Name = "api1",
                    Resources =
                        {
                            "resource_server_1"
                        }
                };

                await openIddictScopeManager.CreateAsync(descriptor);
            }
        }

        // Extended Admin Methods Implementation

        public async Task<List<ApplicationDetailsDto>> GetAllApplicationsAsync()
        {
            var applications = await databaseContext.OpenIddictApplications.ToListAsync();
            var result = new List<ApplicationDetailsDto>();

            foreach (var app in applications)
            {
                result.Add(await MapToApplicationDetailsDto(app));
            }

            return result;
        }

        public async Task<List<ApplicationDetailsDto>> GetResourceServerApplicationsAsync()
        {
            var applications = await databaseContext.OpenIddictApplications.ToListAsync();
            var result = new List<ApplicationDetailsDto>();

            foreach (var app in applications)
            {
                var permissions = await openIddictApplicationManager.GetPermissionsAsync(app);
                if (permissions.Contains(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Introspection))
                {
                    result.Add(await MapToApplicationDetailsDto(app));
                }
            }

            return result;
        }

        public async Task<ApplicationDetailsDto> GetApplicationDetailsAsync(string id)
        {
            var app = await openIddictApplicationManager.FindByIdAsync(id);
            if (app == null)
                return null;

            return await MapToApplicationDetailsDto(app);
        }

        public async Task<(string ClientSecret, string ApplicationId)> CreateApplicationAsync(ApplicationCreateDto dto)
        {
            string clientSecret = dto.ClientSecret;

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                clientSecret = PasswordGenerator.GenerateRandomPassword(new Microsoft.AspNetCore.Identity.PasswordOptions()
                {
                    RequiredLength = 32,
                    RequiredUniqueChars = 4,
                    RequireLowercase = true,
                    RequireUppercase = true,
                    RequireNonAlphanumeric = true,
                    RequireDigit = true
                });
            }

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = dto.ClientId,
                ClientSecret = clientSecret,
                DisplayName = dto.DisplayName
            };

            // Add description if provided
            if (!string.IsNullOrEmpty(dto.Description))
            {
                descriptor.Properties["Description"] = JsonSerializer.SerializeToElement(dto.Description);
            }

            // Add redirect URIs
            foreach (var uri in dto.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }

            // Add post logout redirect URIs
            foreach (var uri in dto.PostLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }

            // Configure permissions and requirements based on client type
            ConfigureClientTypeDefaults(descriptor, dto.ClientType, dto);

            // Add scopes
            foreach (var scope in dto.AllowedScopes)
            {
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
            }

            var app = await openIddictApplicationManager.CreateAsync(descriptor);
            var appId = await openIddictApplicationManager.GetIdAsync(app);

            return (clientSecret, appId.ToString());
        }

        public async Task UpdateApplicationAsync(ApplicationUpdateDto dto)
        {
            var app = await openIddictApplicationManager.FindByIdAsync(dto.Id);
            if (app == null)
                throw new Exception("Application not found");

            var descriptor = new OpenIddictApplicationDescriptor();
            await openIddictApplicationManager.PopulateAsync(descriptor, app);

            // Update basic info
            descriptor.DisplayName = dto.DisplayName;

            // Update description (handle both set and clear cases)
            if (string.IsNullOrEmpty(dto.Description))
            {
                descriptor.Properties.Remove("Description");
            }
            else
            {
                descriptor.Properties["Description"] = JsonSerializer.SerializeToElement(dto.Description);
            }

            // Update redirect URIs
            descriptor.RedirectUris.Clear();
            foreach (var uri in dto.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }

            // Update post logout redirect URIs
            descriptor.PostLogoutRedirectUris.Clear();
            foreach (var uri in dto.PostLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }

            // Update scopes
            var existingScopePermissions = descriptor.Permissions
                .Where(p => p.StartsWith(Permissions.Prefixes.Scope))
                .ToList();

            foreach (var perm in existingScopePermissions)
            {
                descriptor.Permissions.Remove(perm);
            }

            foreach (var scope in dto.AllowedScopes)
            {
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
            }

            // Update AllowOfflineAccess permission
            if (dto.AllowOfflineAccess)
            {
                if (!descriptor.Permissions.Contains(Permissions.GrantTypes.RefreshToken))
                {
                    descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
                }
            }
            else
            {
                descriptor.Permissions.Remove(Permissions.GrantTypes.RefreshToken);
            }

            await openIddictApplicationManager.UpdateAsync(app, descriptor);
        }

        public async Task DeleteApplicationAsync(string id)
        {
            var app = await openIddictApplicationManager.FindByIdAsync(id);
            if (app != null)
            {
                await openIddictApplicationManager.DeleteAsync(app);
            }
        }

        public async Task<List<ScopeDto>> GetAllScopesAsync()
        {
            var scopes = await databaseContext.OpenIddictScopes.ToListAsync();
            var result = new List<ScopeDto>();

            foreach (var scope in scopes)
            {
                var dto = await MapToScopeDto(scope);
                result.Add(dto);
            }

            return result;
        }

        public async Task<ScopeDto> GetScopeAsync(string id)
        {
            var scope = await openIddictScopeManager.FindByIdAsync(id);
            if (scope == null)
                return null;

            return await MapToScopeDto(scope);
        }

        public async Task CreateScopeAsync(ScopeCreateDto dto)
        {
            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = dto.Name,
                DisplayName = dto.DisplayName,
                Description = dto.Description
            };

            foreach (var resource in dto.Resources)
            {
                descriptor.Resources.Add(resource);
            }

            await openIddictScopeManager.CreateAsync(descriptor);
        }

        public async Task UpdateScopeAsync(ScopeUpdateDto dto)
        {
            var scope = await openIddictScopeManager.FindByIdAsync(dto.Id);
            if (scope == null)
                throw new Exception("Scope not found");

            var descriptor = new OpenIddictScopeDescriptor();
            await openIddictScopeManager.PopulateAsync(descriptor, scope);

            descriptor.DisplayName = dto.DisplayName;
            descriptor.Description = dto.Description;

            descriptor.Resources.Clear();
            foreach (var resource in dto.Resources)
            {
                descriptor.Resources.Add(resource);
            }

            await openIddictScopeManager.UpdateAsync(scope, descriptor);
        }

        public async Task DeleteScopeAsync(string id)
        {
            var scope = await openIddictScopeManager.FindByIdAsync(id);
            if (scope != null)
            {
                await openIddictScopeManager.DeleteAsync(scope);
            }
        }

        // Helper methods

        private async Task<ApplicationDetailsDto> MapToApplicationDetailsDto(object app)
        {
            var clientId = await openIddictApplicationManager.GetClientIdAsync(app);
            var displayName = await openIddictApplicationManager.GetDisplayNameAsync(app);
            var type = await openIddictApplicationManager.GetClientTypeAsync(app);
            var permissions = await openIddictApplicationManager.GetPermissionsAsync(app);
            var requirements = await openIddictApplicationManager.GetRequirementsAsync(app);
            var redirectUris = await openIddictApplicationManager.GetRedirectUrisAsync(app);
            var postLogoutUris = await openIddictApplicationManager.GetPostLogoutRedirectUrisAsync(app);

            // Get properties to extract Description
            var properties = await openIddictApplicationManager.GetPropertiesAsync(app);
            string description = null;
            if (properties != null && properties.TryGetValue("Description", out var descElement))
            {
                description = descElement.GetString();
            }

            var dto = new ApplicationDetailsDto
            {
                Id = await openIddictApplicationManager.GetIdAsync(app) as string,
                ClientId = clientId,
                DisplayName = displayName,
                Description = description,
                ClientSecret = "••••••••••••••••", // Always masked
                RedirectUris = redirectUris.Select(u => u.ToString()).ToList(),
                PostLogoutRedirectUris = postLogoutUris.Select(u => u.ToString()).ToList(),
                Permissions = permissions.ToList(),
                Requirements = requirements.ToList(),
                AllowedScopes = permissions
                    .Where(p => p.StartsWith(Permissions.Prefixes.Scope))
                    .Select(p => p.Substring(Permissions.Prefixes.Scope.Length))
                    .ToList(),
                RequirePkce = requirements.Contains(Requirements.Features.ProofKeyForCodeExchange),
                AllowOfflineAccess = permissions.Contains(Permissions.GrantTypes.RefreshToken),
                Status = "Active",
                ClientType = DetermineClientType(type, requirements, permissions)
            };

            return dto;
        }

        private async Task<ScopeDto> MapToScopeDto(object scope)
        {
            var name = await openIddictScopeManager.GetNameAsync(scope);
            var displayName = await openIddictScopeManager.GetDisplayNameAsync(scope);
            var description = await openIddictScopeManager.GetDescriptionAsync(scope);
            var resources = await openIddictScopeManager.GetResourcesAsync(scope);

            return new ScopeDto
            {
                Id = await openIddictScopeManager.GetIdAsync(scope) as string,
                Name = name,
                DisplayName = displayName,
                Description = description,
                Resources = resources.ToList()
            };
        }

        private ClientType DetermineClientType(string type, ImmutableArray<string> requirements, ImmutableArray<string> permissions)
        {
            bool hasPkce = requirements.Contains(Requirements.Features.ProofKeyForCodeExchange);
            bool hasClientSecret = type == OpenIddictConstants.ClientTypes.Confidential;
            bool hasImplicit = permissions.Contains(Permissions.ResponseTypes.IdToken) ||
                              permissions.Contains(Permissions.ResponseTypes.Token);
            bool hasAuthCode = permissions.Contains(Permissions.GrantTypes.AuthorizationCode);
            bool hasClientCredentials = permissions.Contains(Permissions.GrantTypes.ClientCredentials);
            bool hasDeviceCode = permissions.Contains(Permissions.GrantTypes.DeviceCode);
            bool hasIntrospection = permissions.Contains(Permissions.Endpoints.Introspection);

            // Resource Server: has introspection permission only, no grant types
            if (hasIntrospection && !hasClientCredentials && !hasAuthCode && !hasDeviceCode)
                return ClientType.ResourceServer;

            if (hasClientCredentials)
                return ClientType.Machine;

            if (hasDeviceCode)
                return ClientType.Device;

            if (hasImplicit)
                return ClientType.SpaLegacy;

            if (hasAuthCode && hasPkce && !hasClientSecret)
                return ClientType.Spa;

            if (hasAuthCode && hasClientSecret)
                return ClientType.Web;

            if (hasAuthCode && hasPkce)
                return ClientType.Native;

            return ClientType.Web; // Default
        }

        private string GetApplicationType(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.Machine => OpenIddictConstants.ClientTypes.Confidential,
                ClientType.Web => OpenIddictConstants.ClientTypes.Confidential,
                _ => OpenIddictConstants.ClientTypes.Public
            };
        }

        private void ConfigureClientTypeDefaults(OpenIddictApplicationDescriptor descriptor, ClientType clientType, ApplicationCreateDto dto)
        {
            switch (clientType)
            {
                case ClientType.Spa:
                    descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
                    descriptor.Permissions.Add(Permissions.Endpoints.Token);
                    descriptor.Permissions.Add(Permissions.Endpoints.EndSession);
                    descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
                    descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
                    descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
                    if (dto.AllowOfflineAccess == true)
                        descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
                    break;

                case ClientType.Web:
                    descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
                    descriptor.Permissions.Add(Permissions.Endpoints.Token);
                    descriptor.Permissions.Add(Permissions.Endpoints.EndSession);
                    descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
                    descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
                    if (dto.RequirePkce != false)
                        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
                    if (dto.AllowOfflineAccess != false)
                        descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
                    break;

                case ClientType.Native:
                    descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
                    descriptor.Permissions.Add(Permissions.Endpoints.Token);
                    descriptor.Permissions.Add(Permissions.Endpoints.EndSession);
                    descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
                    descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
                    descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
                    if (dto.AllowOfflineAccess == true)
                        descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
                    break;

                case ClientType.Machine:
                    descriptor.Permissions.Add(Permissions.Endpoints.Token);
                    descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
                    break;

                case ClientType.Device:
                    // Note: Device flow requires special endpoints that may need to be configured
                    descriptor.Permissions.Add(Permissions.Endpoints.Token);
                    descriptor.Permissions.Add(Permissions.GrantTypes.DeviceCode);
                    if (dto.AllowOfflineAccess == true)
                        descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
                    break;

                case ClientType.SpaLegacy:
                    descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
                    descriptor.Permissions.Add(Permissions.GrantTypes.Implicit);
                    descriptor.Permissions.Add(Permissions.ResponseTypes.IdToken);
                    descriptor.Permissions.Add(Permissions.ResponseTypes.Token);
                    descriptor.Permissions.Add(Permissions.ResponseTypes.IdTokenToken);
                    break;

                case ClientType.ResourceServer:
                    descriptor.Permissions.Add(Permissions.Endpoints.Introspection);
                    break;
            }

            // Add standard OIDC scopes
            descriptor.Permissions.Add(Permissions.Scopes.Email);
            descriptor.Permissions.Add(Permissions.Scopes.Profile);
            descriptor.Permissions.Add(Permissions.Scopes.Roles);
        }
    }
}