using AuthScape.IDP;
using AuthScape.IDP.Services.ErrorTracking;
using AuthScape.Models.Users;
using AuthScape.Services;
using AuthScape.Services.Azure.Storage;
using AuthScape.Services.Invite;
using CoreBackpack.Azure;
using CoreBackpack.Services;
using IDP.Services;
using IDP.Services.IdentityServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Services;
using Services.Context;
using Services.Database;
using System;

namespace IDP
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _currentEnvironment = env;
            authenticationManager = new AuthenticationManager();
        }

        private AuthenticationManager authenticationManager;
        readonly IWebHostEnvironment _currentEnvironment;

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            authenticationManager.RegisterConfigureServices(Configuration, services, _currentEnvironment, (_appsettings) =>
            {
                services.AddIdentity<AppUser, Role>()
                    .AddEntityFrameworkStores<DatabaseContext>()
                    .AddDefaultTokenProviders()
                    .AddDefaultUI();

                // Configure database with the provider specified in appsettings.json
                // Supports: SqlServer, PostgreSQL, MySQL, SQLite
                services.AddAuthScapeDatabase(
                    _appsettings,
                    enableSensitiveDataLogging: _currentEnvironment.IsDevelopment(),
                    useOpenIddict: true,
                    lifetime: ServiceLifetime.Scoped);

            }, (authBuilder) =>
            {
                services.AddScoped<IInviteService, InviteService>();
                services.AddScoped<IIdentityServerService, IdentityServerService>();
                services.AddScoped<ISSOProviderService, SSOProviderService>();
                services.AddScoped<ISettingsService, SettingsService>();
                services.AddScoped<IRoleService, AuthScape.Services.RoleService>();
                services.AddScoped<IPermissionService, PermissionService>();
                services.AddScoped<IInviteSettingsService, InviteSettingsService>();
                services.AddScoped<IAzureBlobStorage, AzureBlobStorage>();
                services.AddScoped<IBlobStorage, BlobStorage>();
                services.AddScoped<IImageService, ImageService>();

                services.AddScoped<IErrorTrackingService, ErrorTrackingService>();
                services.AddScoped<IErrorGroupingService, ErrorGroupingService>();

                // Add SignalR for real-time error tracking updates
                services.AddSignalR();

                services.AddHttpClient();

                ThirdPartyAuthService.AddThirdPartyAutentication(services);

            }, "", "");
        }

        public void Configure(IApplicationBuilder app)
        {
            authenticationManager.RegisterConfigure(app);
        }
    }
}
