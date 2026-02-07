using AuthScape.AI.Configuration;
using AuthScape.AI.Memory;
using AuthScape.AI.Providers;
using AuthScape.AI.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthScape.AI.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers AuthScape.AI services with inline configuration.
    /// </summary>
    public static AuthScapeAIBuilder AddAuthScapeAI(
        this IServiceCollection services,
        Action<AIServiceOptions> configure)
    {
        services.Configure(configure);
        return RegisterCore(services);
    }

    /// <summary>
    /// Registers AuthScape.AI services from IConfiguration (appsettings.json).
    /// </summary>
    public static AuthScapeAIBuilder AddAuthScapeAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AIServiceOptions>(
            configuration.GetSection(AIServiceOptions.SectionName));
        return RegisterCore(services);
    }

    private static AuthScapeAIBuilder RegisterCore(IServiceCollection services)
    {
        var builder = new AuthScapeAIBuilder(services);

        // Register all providers - they self-check IsConfigured
        services.AddSingleton<IAIProvider, OllamaProvider>();
        services.AddSingleton<IAIProvider, OpenAIProvider>();
        services.AddSingleton<IAIProvider, AnthropicProvider>();
        services.AddSingleton<IAIProvider, GoogleProvider>();
        services.AddSingleton<IAIProvider, MistralProvider>();
        services.AddSingleton<IAIProvider, OpenAICompatibleProvider>();

        services.AddSingleton<IAIToolRegistry>(sp =>
        {
            var registry = new AIToolRegistry();

            // Register IAITool implementations from DI
            foreach (var tool in sp.GetServices<IAITool>())
            {
                registry.Register(tool);
            }

            // Register from annotated types
            foreach (var type in builder.ToolTypes)
            {
                registry.RegisterFromType(type);
            }

            // Register from assemblies
            foreach (var assembly in builder.ToolAssemblies)
            {
                registry.RegisterFromAssembly(assembly);
            }

            return registry;
        });

        services.AddSingleton<IAIService>(sp =>
        {
            var providers = sp.GetServices<IAIProvider>();
            var toolRegistry = sp.GetRequiredService<IAIToolRegistry>();
            var logger = sp.GetRequiredService<ILogger<AIService>>();
            var options = sp.GetRequiredService<IOptions<AIServiceOptions>>();

            // Collect additional KernelPlugins from builder
            var plugins = builder.Plugins.Count > 0 ? builder.Plugins : null;

            // Conditionally create memory service
            IAIMemoryService? memory = null;
            if (builder.MemoryEnabled)
            {
                memory = sp.GetService<IAIMemoryService>();
            }

            return new AIService(providers, toolRegistry, logger,
                options.Value.DefaultProvider, plugins, memory);
        });

        return builder;
    }
}
