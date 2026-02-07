using System.Reflection;
using AuthScape.AI.Enums;
using AuthScape.AI.Memory;
using AuthScape.AI.Providers;
using AuthScape.AI.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace AuthScape.AI;

/// <summary>
/// Fluent builder for configuring AuthScape.AI services and tools.
/// </summary>
public class AuthScapeAIBuilder
{
    public IServiceCollection Services { get; }

    internal List<Assembly> ToolAssemblies { get; } = [];
    internal List<Type> ToolTypes { get; } = [];
    internal List<KernelPlugin> Plugins { get; } = [];
    internal bool MemoryEnabled { get; private set; }

    public AuthScapeAIBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>Registers an IAITool implementation via DI.</summary>
    public AuthScapeAIBuilder WithTool<TTool>() where TTool : class, IAITool
    {
        Services.AddSingleton<IAITool, TTool>();
        return this;
    }

    /// <summary>Registers all methods decorated with [AITool] in the given type.</summary>
    public AuthScapeAIBuilder WithToolsFrom<TClass>() where TClass : class
    {
        ToolTypes.Add(typeof(TClass));
        return this;
    }

    /// <summary>Registers all methods decorated with [AITool] in the given assembly.</summary>
    public AuthScapeAIBuilder WithToolsFromAssembly(Assembly assembly)
    {
        ToolAssemblies.Add(assembly);
        return this;
    }

    /// <summary>Registers a native Semantic Kernel plugin alongside AuthScape tools.</summary>
    public AuthScapeAIBuilder WithPlugin(KernelPlugin plugin)
    {
        Plugins.Add(plugin);
        return this;
    }

    /// <summary>Enables the Memory/RAG subsystem backed by SK InMemoryVectorStore.</summary>
    public AuthScapeAIBuilder WithMemory()
    {
        MemoryEnabled = true;

        Services.AddSingleton<IAIMemoryService>(sp =>
        {
            var aiProviders = sp.GetServices<IAIProvider>();

            var embeddingProvider = aiProviders
                .Where(p => p.IsConfigured)
                .FirstOrDefault(p => p.Capabilities.Supports(AICapability.Embeddings));

            if (embeddingProvider is null)
                throw new InvalidOperationException(
                    "Memory requires at least one provider that supports embeddings (OpenAI, Ollama).");

            var kernelBuilder = Kernel.CreateBuilder();
            embeddingProvider.ConfigureKernel(kernelBuilder);
            var kernel = kernelBuilder.Build();

            var embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            return new AIMemoryService(embeddingGenerator);
        });

        return this;
    }
}
