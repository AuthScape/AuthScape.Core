using AuthScape.AI.Enums;
using AuthScape.AI.Models;
using Microsoft.SemanticKernel;

namespace AuthScape.AI.Providers;

/// <summary>
/// Abstraction for an AI provider backend. Each provider configures
/// an SK Kernel with its AI services.
/// </summary>
public interface IAIProvider : IDisposable
{
    /// <summary>Which provider this is.</summary>
    AIProvider ProviderType { get; }

    /// <summary>What this provider supports.</summary>
    ProviderCapabilities Capabilities { get; }

    /// <summary>Whether the provider is properly configured and ready.</summary>
    bool IsConfigured { get; }

    /// <summary>Configures an SK KernelBuilder with this provider's AI services.</summary>
    void ConfigureKernel(IKernelBuilder builder);

    /// <summary>Lists models available from this provider.</summary>
    Task<IReadOnlyList<AIModel>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets model details if supported.</summary>
    Task<AIModelDetails?> GetModelDetailsAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>Gets the native/underlying client object for direct access.</summary>
    object? GetNativeClient();
}
