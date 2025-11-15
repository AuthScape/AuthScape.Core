using AuthScape.IDP;
using AuthScape.Models.Users;
using AuthScape.SendGrid;
using AuthScape.Services;
using AuthScape.Services.Mail.Configuration;
using AuthScape.StripePayment.Services;
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

                // database connections
                if (_currentEnvironment.IsDevelopment())
                {
                    services.AddDbContext<DatabaseContext>(options =>
                    {
                        // Configure the context to use Microsoft SQL Server.
                        options.UseSqlServer(_appsettings.DatabaseContext,
                            sqlServerOptionsAction: sqlOptions =>
                            {
                                // will attempt to reconnect the connection
                                sqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 10,
                                maxRetryDelay: TimeSpan.FromSeconds(30),
                                errorNumbersToAdd: null);
                            });

                        // Register the entity sets needed by OpenIddict.
                        // Note: use the generic overload if you need
                        // to replace the default OpenIddict entities.
                        options.UseOpenIddict();
                    });
                }
                else if (_currentEnvironment.IsStaging())
                {
                    services.AddDbContext<DatabaseContext>(options =>
                    {
                        // Configure the context to use Microsoft SQL Server.
                        options.UseSqlServer(_appsettings.DatabaseContext,
                            sqlServerOptionsAction: sqlOptions =>
                            {
                                // will attempt to reconnect the connection
                                sqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 10,
                                maxRetryDelay: TimeSpan.FromSeconds(30),
                                errorNumbersToAdd: null);
                            });

                        // Register the entity sets needed by OpenIddict.
                        // Note: use the generic overload if you need
                        // to replace the default OpenIddict entities.
                        options.UseOpenIddict();
                    });
                }
                else if (_currentEnvironment.IsProduction())
                {
                    services.AddDbContext<DatabaseContext>(options =>
                    {
                        // Configure the context to use Microsoft SQL Server.
                        options.UseSqlServer(_appsettings.DatabaseContext,
                            sqlServerOptionsAction: sqlOptions =>
                            {
                                // will attempt to reconnect the connection
                                sqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 10,
                                maxRetryDelay: TimeSpan.FromSeconds(30),
                                errorNumbersToAdd: null);
                            });

                        // Register the entity sets needed by OpenIddict.
                        // Note: use the generic overload if you need
                        // to replace the default OpenIddict entities.
                        options.UseOpenIddict();
                    });
                }

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


            }, "DRjd/GnduI3Efzen9V9BvbNUfc/VKgXltV7Kbk9sMkY=", "5f1943fbb39bd93b92b34b2a67543aae6b538ed8");
        }

        public void Configure(IApplicationBuilder app)
        {
            authenticationManager.RegisterConfigure(app);
        }
    }
}