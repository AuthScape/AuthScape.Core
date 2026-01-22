using AuthScape.Configuration.Extensions;
using AuthScape.UserManageSystem.CRM.Extensions;
using AuthScape.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Services;
using Services.Context;
using Services.Cores;
using Services.Database;
using Services.Tracking;
using System;
using System.Threading.Tasks;

namespace AuthScape.Controllers
{
    public class AuthenticationManager
    {
        public void RegisterConfigureServices(IConfiguration Configuration, IWebHostEnvironment _currentEnvironment, IServiceCollection services,
            Action<OpenIddictBuilder> Builder, Action<IServiceCollection> scope, Action<AppSettings, IWebHostEnvironment, IServiceCollection> dbContextSetup)
        {
            // Add AuthScape settings with validation (uses shared configuration from authscape.json)
            services.AddAuthScapeSettings(Configuration, options =>
            {
                options.ValidateOnStartup = !_currentEnvironment.IsDevelopment();
            });

            var _appsettings = Configuration.GetAuthScapeSettings();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });
            // Register the OpenIddict validation components.
            var builder = services.AddOpenIddict()
                .AddCore(options =>
                {
                    options.UseEntityFrameworkCore()
                            .UseDbContext<DatabaseContext>();
                });

            Builder(builder);


            dbContextSetup(_appsettings, _currentEnvironment, services);



            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<ISlugService, SlugService>();
            services.AddScoped<IUserManagementService, UserManagementService>();

            // Add CRM integration services
            services.AddAuthScapeCrm();

            scope(services);


            // clean the payload from null values
            services.AddControllers()
                .AddApplicationPart(typeof(AuthScape.UserManageSystem.CRM.Controllers.CrmConnectionController).Assembly)
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.MaxDepth = 64;
                });

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
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, Action<IEndpointRouteBuilder>? useEndpoints = null)
        {
            ApplyMigration(app);

            // ErrorTrackingMiddleware MUST be first to catch all exceptions before other handlers
            app.UseMiddleware(typeof(ErrorTrackingMiddleware));

            app.UseCors("default");

            if (!env.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                if (env.IsDevelopment())
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

        private void ApplyMigration(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope();
            scope.ServiceProvider.GetRequiredService<DatabaseContext>().Database.Migrate();
        }
    }
}