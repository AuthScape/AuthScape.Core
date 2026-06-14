using AuthScape.AccountLinking;
using AuthScape.Analytics.Services;
using AuthScape.Configuration.Extensions;
using AuthScape.Ldap;
using AuthScape.Notifications;
using AuthScape.Saml2;
using AuthScape.Scim;
using AuthScape.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Services;
using AuthScape.Models.Users;
using AuthScape.Services.Database;
using Models.Authentication;
using Services.Context;
using Services.Cores;
using Services.Database;
using Services.Tracking;
using System;
using System.Threading.Tasks;
using AuthScape.Logging;

namespace AuthScape.Controllers
{
    public class AuthenticationManager
    {
        /// <summary>
        /// Register the shared AuthScape API stack. Provider-agnostic — the chosen token issuer
        /// (OpenIddict or Keycloak) is configured separately via
        /// <c>services.AddAuthScapeIdentity().UseOpenIddict(...)</c> or
        /// <c>services.AddAuthScapeIdentity().UseKeycloak(...)</c> earlier in the host's Startup.
        /// Pass <paramref name="scope"/> to register your app's own services. The database, identity
        /// stores, federation plugins, analytics, and logging are all wired internally.
        /// </summary>
        public void RegisterConfigureServices(
            IConfiguration Configuration,
            IWebHostEnvironment _currentEnvironment,
            IServiceCollection services,
            Action<IServiceCollection>? scope = null)
        {
            // Add AuthScape settings. Strict validation is enforced in Staging and Production so a
            // bad config fails fast; Development boots even with incomplete settings so devs can
            // iterate.
            services.AddAuthScapeSettings(Configuration, options =>
            {
                options.ValidateOnStartup = _currentEnvironment.IsStaging() || _currentEnvironment.IsProduction();
            });

            var _appsettings = Configuration.GetAuthScapeSettings();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });

            // ASP.NET Identity stores are provider-agnostic: both OpenIddict and Keycloak provisioning
            // paths create AppUser rows via UserManager.
            services.AddIdentity<AppUser, Role>()
                .AddEntityFrameworkStores<DatabaseContext>()
                .AddDefaultTokenProviders();

            // Database — provider auto-detected from the connection string (SqlServer, PostgreSQL,
            // MySQL, or SQLite). The OpenIddict token-issuer tables are mapped only when the host is
            // running the OpenIddict provider; on the Keycloak path they are excluded from the model
            // entirely, so they are never created. Driven by the same "Authentication:Provider" knob
            // that selects the runtime provider.
            var authProvider = Configuration["Authentication:Provider"] ?? "OpenIddict";
            var useOpenIddict = string.Equals(authProvider, "OpenIddict", StringComparison.OrdinalIgnoreCase);

            services.AddAuthScapeDatabase(
                _appsettings,
                enableSensitiveDataLogging: _currentEnvironment.IsDevelopment(),
                useOpenIddict: useOpenIddict,
                lifetime: ServiceLifetime.Scoped);

            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // === Authentication core (always-on; the host should not have to re-register these) ===
            services.AddScoped<ISlugService, SlugService>();
            services.AddScoped<CoreBackpack.Services.ISlugService, CoreBackpack.Services.SlugService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<IInviteService, InviteService>();
            services.AddScoped<IRoleService, RoleService>();

            // === Federation plugins (inert until per-tenant config rows exist in the DB) ===
            services.AddAuthScapeAccountLinking();

            // LDAP / SAML / SCIM are OpenIddict-coupled federation features: LDAP and SAML exchange
            // credentials for tokens via OpenIddict's /connect/token, and SCIM authenticates with
            // OpenIddict client_credentials. On the Keycloak path Keycloak natively owns LDAP user
            // federation and SAML brokering, so these are skipped entirely — services here, their
            // controllers (removed below), and their config tables (excluded from the EF model).
            if (useOpenIddict)
            {
                services.AddAuthScapeLdap();
                services.AddAuthScapeSaml2();
                services.AddAuthScapeScim();
            }

            // === Always-on platform services ===
            services.AddScoped<IAnalyticsService, AnalyticsService>();
            services.AddScoped<ILogService, LogService>();
            services.AddScoped<INotificationService, NotificationService>();

            // ErrorTracking forwards from the API to the IDP over HTTP.
            services.AddHttpClient();

            // Keycloak admin integration. Always registered; short-circuits when
            // AppSettings.Keycloak.Enabled is false. Safe to keep on the OpenIddict path too.
            services.AddHttpClient<AuthScape.Services.Keycloak.IKeycloakAdminService, AuthScape.Services.Keycloak.KeycloakAdminService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            // SignalR is registered but no hubs are mapped by the core — the host's Configure()
            // callback maps hubs only for the modules it has opted in to.
            services.AddSignalR();

            scope?.Invoke(services);

            // clean the payload from null values
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.MaxDepth = 64;
                });

            // On the Keycloak path the OpenIddict-coupled federation services above are not registered,
            // so drop their controllers (LdapAuth, SamlAuth, Scim*) from MVC discovery — otherwise they
            // would surface in routing/Swagger and fail at activation with missing dependencies.
            if (!useOpenIddict)
            {
                var federationAssemblies = new[]
                {
                    typeof(AuthScape.Ldap.ServiceCollectionExtensions).Assembly,
                    typeof(AuthScape.Saml2.ServiceCollectionExtensions).Assembly,
                    typeof(AuthScape.Scim.ServiceCollectionExtensions).Assembly,
                };

                services.AddControllers().ConfigureApplicationPartManager(apm =>
                {
                    foreach (var part in apm.ApplicationParts
                        .OfType<AssemblyPart>()
                        .Where(p => federationAssemblies.Contains(p.Assembly))
                        .ToList())
                    {
                        apm.ApplicationParts.Remove(part);
                    }
                });
            }

            services.AddEndpointsApiExplorer();
            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Info = new()
                    {
                        Title = "AuthScape API",
                        Version = "v1",
                        Description = "AuthScape API Documentation"
                    };
                    return Task.CompletedTask;
                });
            });

            // Configure the OpenAPI JSON serializer to handle circular references
            services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            {
                options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                options.SerializerOptions.MaxDepth = 128;
            });

            services.AddCors();
            services.AddTransient<ICorsPolicyProvider, CorsPolicyManager>();

            // Global rate limiter, per client (authenticated 'sub' claim, or IP for anonymous).
            // Configurable via appsettings.json "RateLimiting" section. Returns HTTP 429 on excess.
            var permitLimit   = Configuration.GetValue<int?>("RateLimiting:PermitLimit")   ?? 100;
            var windowSeconds = Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 60;
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var key = context.User?.FindFirst("sub")?.Value
                              ?? context.Connection.RemoteIpAddress?.ToString()
                              ?? "unknown";
                    return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(key, _ =>
                        new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitLimit,
                            Window = TimeSpan.FromSeconds(windowSeconds),
                            QueueLimit = 0,
                            QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true,
                        });
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, Action<IEndpointRouteBuilder>? useEndpoints = null)
        {
            EnsureDatabaseSchema(app);

            // ErrorTrackingMiddleware MUST be first to catch all exceptions before other handlers
            app.UseMiddleware(typeof(ErrorTrackingMiddleware));

            // Security response headers (HSTS + CSP + XFO, strips Server/X-Powered-By)
            app.UseMiddleware<SecurityHeadersMiddleware>();

            app.UseCors("default");

            // HTTPS redirect: enforce in Staging and Production. Development runs over HTTP for
            // local dev convenience.
            if (env.IsStaging() || env.IsProduction())
            {
                app.UseHttpsRedirection();
            }

            // HSTS: turn on only in Production (Staging may use self-signed / short-lived certs).
            if (env.IsProduction())
            {
                app.UseHsts();
            }

            app.UseRouting();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                // Auth-core SignalR hub for in-app notifications. Hosts can map additional hubs
                // for opted-in modules via the useEndpoints callback below.
                endpoints.MapHub<NotificationHub>("/notifications");

                // API docs (Scalar / OpenAPI) are exposed in Development and Staging so QA can
                // exercise the surface, but never in Production.
                if (env.IsDevelopment() || env.IsStaging())
                {
                    endpoints.MapOpenApi();
                    endpoints.MapScalarApiReference();
                }

                if (useEndpoints != null)
                {
                    useEndpoints(endpoints);
                }
            });
        }

        // Creates the database and schema from the live EF model if missing — provider-agnostic
        // (SQL Server / PostgreSQL / SQLite). This replaces EF migrations, which were SQL-Server-only.
        private void EnsureDatabaseSchema(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            DatabaseProviderExtensions.EnsureDatabase(context);
        }
    }
}
