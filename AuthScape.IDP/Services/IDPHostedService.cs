using AuthScape.Models.Users;
using AuthScape.Services.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using Services.Context;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IDP.Services
{
    public class IDPHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public IDPHostedService(IServiceProvider serviceProvider)
            => _serviceProvider = serviceProvider;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            await DatabaseProviderExtensions.EnsureDatabaseAsync(context, cancellationToken);

            await CreateApplicationsAsync();
            await CreateScopesAsync();
            await CreateRolesAsync();

            async Task CreateApplicationsAsync()
            {
                var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

                if (await manager.FindByClientIdAsync("postman", cancellationToken) is null)
                {
                    await manager.CreateAsync(new OpenIddictApplicationDescriptor
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
                    }, cancellationToken);
                }


                if (await manager.FindByClientIdAsync("resource_server_1") == null)
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

                    await manager.CreateAsync(descriptor);
                }

                if (await manager.FindByClientIdAsync("rust_server") == null)
                {
                    var descriptor = new OpenIddictApplicationDescriptor
                    {
                        ClientId = "rust_server",
                        ClientSecret = "A1F3E2D4-8B7C-4A9E-B6D5-3C2E1F0A9B8D",
                        Permissions =
                        {
                            Permissions.Endpoints.Introspection
                        }
                    };

                    await manager.CreateAsync(descriptor);
                }

                // Note: no client registration is created for resource_server_2
                // as it uses local token validation instead of introspection.

                // PageTypes seeding moved to the ContentManagement module — not part of the auth core.
            }

            async Task CreateScopesAsync()
            {
                var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

                if (await manager.FindByNameAsync("api1") == null)
                {
                    var descriptor = new OpenIddictScopeDescriptor
                    {
                        Name = "api1",
                        Resources =
                        {
                            "resource_server_1",
                            "rust_server"
                        }
                    };

                    await manager.CreateAsync(descriptor);
                }
            }

            async Task CreateRolesAsync()
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

                // Seed default roles
                string[] defaultRoles = { "Admin", "User" };

                foreach (var roleName in defaultRoles)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        await roleManager.CreateAsync(new Role { Name = roleName });
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}