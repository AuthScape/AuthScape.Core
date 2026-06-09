using AuthScape.Configuration.Extensions;
using AuthScape.Document.Models;
using AuthScape.IDP.Services;
using AuthScape.Models.Users;
using AuthScape.Services;
using IDP.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Services;
using Services.Context;
using Services.Cores;
using Services.Database;
using Services.Tracking;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthScape.IDP
{
    public class AuthenticationManager
    {
        public void RegisterConfigureServices(IConfiguration Configuration, IServiceCollection services, IWebHostEnvironment _currentEnvironment, Action<AppSettings> databaseConnection, Action<AuthenticationBuilder> authBuilder,
            string signingCertificateThumbprint, string encyptionCertificateThumbprint)
        {
            // Add AuthScape settings with validation (uses shared configuration from authscape.json)
            services.AddAuthScapeSettings(Configuration, options =>
            {
                options.ValidateOnStartup = !_currentEnvironment.IsDevelopment();
            });

            var _appsettings = Configuration.GetAuthScapeSettings();

            databaseConnection(_appsettings);

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.SameSite = SameSiteMode.None;
            });

            // Configure Identity to use the same JWT claims as OpenIddict instead
            // of the legacy WS-Federation claims it uses by default (ClaimTypes),
            // which saves you from doing the mapping in your authorization controller.
            services.Configure<IdentityOptions>(options =>
            {
                options.ClaimsIdentity.UserNameClaimType = Claims.Name;
                options.ClaimsIdentity.UserIdClaimType = Claims.Subject;
                options.ClaimsIdentity.RoleClaimType = Claims.Role;
            });

            services.AddDataProtection()
               .PersistKeysToDbContext<DatabaseContext>()
               .SetApplicationName(System.Reflection.Assembly.GetEntryAssembly().GetName().Name);

            services.AddOpenIddict()

                // Register the OpenIddict core components.
                .AddCore(options =>
                {
                    // Configure OpenIddict to use the Entity Framework Core stores and models.
                    // Note: call ReplaceDefaultEntities() to replace the default OpenIddict entities.
                    options.UseEntityFrameworkCore()
                           .UseDbContext<DatabaseContext>();
                })

                // Register the OpenIddict server components.
                .AddServer(options =>
                {
                    // Enable the authorization, logout, userinfo, and introspection endpoints.
                    options.SetAuthorizationEndpointUris("/connect/authorize")
                           .SetTokenEndpointUris("/connect/token")
                           .SetEndSessionEndpointUris("/connect/logout")
                           .SetIntrospectionEndpointUris("/connect/introspect")
                           .SetUserInfoEndpointUris("/connect/userinfo");

                    // Mark the "email", "profile" and "roles" scopes as supported scopes.
                    options.RegisterScopes(Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess);

                    // Note: the sample only uses the implicit flow but you can enable the other
                    // flows if you need to support code, password or client credentials.
                    //options.AllowImplicitFlow();

                    options.AllowAuthorizationCodeFlow()
                        .RequireProofKeyForCodeExchange()
                        .AllowClientCredentialsFlow()
                        .AllowRefreshTokenFlow();

                    // Disallow PKCE 'plain' code_challenge_method — S256 only.
                    // 1) Rewrite discovery response so 'code_challenge_methods_supported' contains only 'S256'.
                    options.AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.ApplyConfigurationResponseContext>(builder =>
                        builder.UseInlineHandler(context =>
                        {
                            var element = System.Text.Json.JsonSerializer.SerializeToElement(
                                new[] { CodeChallengeMethods.Sha256 });
                            context.Response.SetParameter(
                                Metadata.CodeChallengeMethodsSupported,
                                new OpenIddict.Abstractions.OpenIddictParameter(element));
                            return ValueTask.CompletedTask;
                        }));
                    // 2) Reject authorization requests that use code_challenge_method=plain.
                    options.AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.ValidateAuthorizationRequestContext>(builder =>
                        builder.UseInlineHandler(context =>
                        {
                            if (string.Equals(context.Request.CodeChallengeMethod, CodeChallengeMethods.Plain, StringComparison.Ordinal))
                            {
                                context.Reject(
                                    error: Errors.InvalidRequest,
                                    description: "The 'plain' code_challenge_method is not supported. Use 'S256'.");
                            }
                            return default;
                        }));



                    //options.AllowRefreshTokenFlow();

                    options.SetAccessTokenLifetime(TimeSpan.FromHours(1));
                    //options.SetAccessTokenLifetime(TimeSpan.FromSeconds(30));

                    // Register the encryption credentials. This sample uses a symmetric
                    // encryption key that is shared between the server and the Api2 sample
                    // (that performs local token validation instead of using introspection).
                    //f
                    // Note: in a real world application, this encryption key should be
                    // stored in a safe place (e.g in Azure KeyVault, stored as a secret).
                    if (_currentEnvironment.IsDevelopment())
                    {
                        options.AddDevelopmentEncryptionCertificate();
                        options.AddDevelopmentSigningCertificate();
                    }
                    else if (OperatingSystem.IsWindows())
                    {
                        options.AddEncryptionCertificate(encyptionCertificateThumbprint);
                        options.AddSigningCertificate(signingCertificateThumbprint);
                    }
                    else
                    {
                        X509Certificate2? signingCert = null;
                        X509Certificate2? encryptionCert = null;

                        // 1. Try app setting base64 (Linux App Service)
                        var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD");
                        var signingBase64 = Environment.GetEnvironmentVariable("SIGNING_CERT_BASE64");
                        if (!string.IsNullOrEmpty(signingBase64))
                        {
                            var certBytes = Convert.FromBase64String(signingBase64);
                            signingCert = new X509Certificate2(certBytes, certPassword,
                                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
                        }

                        var encryptionBase64 = Environment.GetEnvironmentVariable("ENCRYPTION_CERT_BASE64");
                        if (!string.IsNullOrEmpty(encryptionBase64))
                        {
                            var certBytes = Convert.FromBase64String(encryptionBase64);
                            encryptionCert = new X509Certificate2(certBytes, certPassword,
                                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
                        }

                        // 2. Fallback: try X509Store CurrentUser
                        if (signingCert == null || encryptionCert == null)
                        {
                            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                            store.Open(OpenFlags.ReadOnly);

                            if (signingCert == null)
                            {
                                var matches = store.Certificates
                                    .Find(X509FindType.FindByThumbprint, signingCertificateThumbprint, false);
                                if (matches.Count > 0) signingCert = matches[0];
                            }

                            if (encryptionCert == null)
                            {
                                var matches = store.Certificates
                                    .Find(X509FindType.FindByThumbprint, encyptionCertificateThumbprint, false);
                                if (matches.Count > 0) encryptionCert = matches[0];
                            }

                            store.Close();
                        }

                        if (signingCert == null)
                            throw new InvalidOperationException(
                                $"Signing certificate not found. Thumbprint: '{signingCertificateThumbprint}'");

                        if (encryptionCert == null)
                            throw new InvalidOperationException(
                                $"Encryption certificate not found. Thumbprint: '{encyptionCertificateThumbprint}'");

                        options.AddSigningCertificate(signingCert);
                        options.AddEncryptionCertificate(encryptionCert);
                    }

                    // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
                    options.UseAspNetCore()
                           .EnableTokenEndpointPassthrough()
                           .EnableAuthorizationEndpointPassthrough()
                           .EnableEndSessionEndpointPassthrough()
                           .EnableUserInfoEndpointPassthrough()
                           .EnableStatusCodePagesIntegration();
                })

                // Register the OpenIddict validation components.
                .AddValidation(options =>
                {
                    // Import the configuration from the local OpenIddict server instance.
                    options.UseLocalServer();

                    // Register the ASP.NET Core host.
                    options.UseAspNetCore();
                });


            


            authBuilder(services.AddAuthentication());


            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(2);
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });

            var idpUri = new Uri(_appsettings.IDPUrl);
            services.AddFido2(options =>
            {
                options.ServerDomain = idpUri.Host;
                options.ServerName = "Your App";
                options.Origins = new HashSet<string> { idpUri.GetLeftPart(UriPartial.Authority) };
                options.TimestampDriftTolerance = 300000; // 5 minutes
            });


            services.AddCors();
            services.AddControllersWithViews();

            //services.AddTransient<ISendGridService, SendGridService>();
            //services.AddTransient<ITwilioService, TwilioService>();

            // IMailService registration removed with the Email module.
            services.AddScoped<IUserManagementService, UserManagementService>();



            // Register the worker responsible of seeding the database with the sample clients.
            // Note: in a real world application, this step should be part of a setup script.
            services.AddHostedService<IDPHostedService>();


            services.AddTransient<ICorsPolicyProvider, CorsPolicyManager>();


            #region Experimental MultiFactor Authentication

            services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, AdditionalUserClaimsPrincipalFactory>();
            services.AddAuthorization(options => options.AddPolicy("TwoFactorEnabled", x => x.RequireClaim("amr", "mfa")));

            #endregion

            #region Change expire time for tokens 

            if (_appsettings.DataProtectionTokenProviderOptions_TokenLifespanByDays != null)
            {
                services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromDays(_appsettings.DataProtectionTokenProviderOptions_TokenLifespanByDays.Value));
            }

            #endregion

            services.AddRazorPages();

            // Add SignalR for real-time error tracking notifications
            services.AddSignalR();

            //ThirdPartyAuthService.AddThirdPartyAutentication(services);
        }

        public void RegisterConfigure(IApplicationBuilder app)
        {
            // ErrorTrackingMiddleware MUST be first to catch all exceptions before other handlers
            app.UseMiddleware(typeof(ErrorTrackingMiddleware));

            // Security response headers (HSTS + CSP + XFO, strips Server/X-Powered-By)
            app.UseMiddleware<SecurityHeadersMiddleware>();

            // Internally rewrite unprefixed Identity paths (/Account/Login, /Account/Register, etc.)
            // to the Identity area (/Identity/Account/...). Same-method, same-body, no client redirect —
            // so POSTs hit the real login handler (and its lockout logic) instead of 307-bouncing.
            app.Use(async (ctx, next) =>
            {
                var path = ctx.Request.Path;
                if (path.HasValue && path.Value.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Request.Path = "/Identity" + path.Value;
                }
                await next();
            });

            app.UseCors("default");

            app.UseSession();

            app.UseStaticFiles();

            app.UseStatusCodePagesWithReExecute("/error");

            app.UseRouting();


            var supportedCultures = CultureInfo
                .GetCultures(CultureTypes.SpecificCultures | CultureTypes.NeutralCultures)
                .OrderBy(c => c.Name)
                .ToList();

            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture("en-US")
                .AddSupportedCultures(supportedCultures.Select(c => c.Name).ToArray())
                .AddSupportedUICultures(supportedCultures.Select(c => c.Name).ToArray());

            app.UseRequestLocalization(localizationOptions);


            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(options =>
            {
                options.MapControllers();
                options.MapDefaultControllerRoute();
                options.MapRazorPages();
                options.MapHub<AuthScape.IDP.Hubs.ErrorTrackingHub>("/errortracking");
            });
        }
    }
}
