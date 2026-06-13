using AuthScape.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API
{
    public class Startup
    {
        readonly IWebHostEnvironment _currentEnvironment;
        private AuthenticationManager authenticationManager;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _currentEnvironment = env;
            authenticationManager = new AuthenticationManager();
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Pick the token issuer from configuration ("Authentication" section in appsettings).
            // Switch between OpenIddict and Keycloak by changing "Authentication:Provider" — no code
            // change or rebuild. See appsettings.json for the per-provider settings.
            services.AddConfiguredAuthScapeIdentity(Configuration);

            // The auth core (users, companies, invites, roles, federation, analytics, logging,
            // database, identity stores, KeycloakAdmin client, SignalR) is wired by AuthenticationManager.
            // Pass an optional scope callback to register YOUR app's services — omit it if you have none yet.
            authenticationManager.RegisterConfigureServices(Configuration, _currentEnvironment, services);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            authenticationManager.Configure(app, env);
        }
    }
}
