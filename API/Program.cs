using AuthScape.Configuration.Extensions;
using AuthScape.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Add AuthScape configuration with explicit source priority
                    // Use ConfigurationSource.ProjectOnly to only load from appsettings.json
                    config.AddAuthScapeConfiguration(
                        context.HostingEnvironment,
                        ConfigurationSource.ProjectOnly);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
