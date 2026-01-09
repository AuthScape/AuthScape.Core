using AuthScape.IDP;
using AuthScape.Models.Users;
using AuthScape.SendGrid;
using AuthScape.Services;
using AuthScape.Services.Azure.Storage;
using AuthScape.Services.Mail.Configuration;
using AuthScape.Services.Subscription;
using AuthScape.Services.PromoCode;
using AuthScape.Services.Invite;
using AuthScape.StripePayment.Services;
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
                services.AddScoped<IUserService, UserService>();
                services.AddScoped<ISendGridService, SendGridService>();

                // Add universal email service
                services.AddEmailService(Configuration);

                services.AddScoped<IStripePayService, StripePayService>();
                services.AddScoped<IStripeSubscriptionService, StripeSubscriptionService>();
                services.AddScoped<IStripeInvoiceService, StripeInvoiceService>();
                services.AddScoped<IInviteService, InviteService>();
                services.AddScoped<IWalletResolver, WalletResolver>();
                services.AddScoped<IIdentityServerService, IdentityServerService>();
                services.AddScoped<ISSOProviderService, SSOProviderService>();
                services.AddScoped<ISettingsService, SettingsService>();
                services.AddScoped<IRoleService, AuthScape.Services.RoleService>();
                services.AddScoped<IPermissionService, PermissionService>();
                services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
                services.AddScoped<IPromoCodeService, PromoCodeService>();
                services.AddScoped<IInviteSettingsService, InviteSettingsService>();
                services.AddScoped<IAchVerificationEmailService, AchVerificationEmailService>();
                services.AddScoped<IAzureBlobStorage, AzureBlobStorage>();
                services.AddScoped<IBlobStorage, BlobStorage>();
                services.AddScoped<IImageService, ImageService>();

                // Add HttpClientFactory for MCP proxy calls
                services.AddHttpClient();

                ThirdPartyAuthService.AddThirdPartyAutentication(services);


                //authBuilder
                //    .AddFacebook(facebookOptions =>
                //    {
                //        facebookOptions.AppId = "test";
                //        facebookOptions.AppSecret = "test";
                //    });
                //    .AddGoogle((googleOOptions) =>
                //    {
                //        googleOOptions.ClientId = "";
                //        googleOOptions.ClientSecret = "";
                //    });


            }, "", "");
        }

        public void Configure(IApplicationBuilder app)
        {
            authenticationManager.RegisterConfigure(app);
        }
    }
}