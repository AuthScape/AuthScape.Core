using AuthScape.AI.Enums;
using AuthScape.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AuthScape.AI.Providers;

/// <summary>
/// Abstract base class for AI providers with common logging and lifecycle.
/// </summary>
public abstract class AIProviderBase : IAIProvider
{
    protected readonly ILogger Logger;
    private bool _disposed;

    public abstract AIProvider ProviderType { get; }
    public abstract ProviderCapabilities Capabilities { get; }
    public abstract bool IsConfigured { get; }

    protected AIProviderBase(ILogger logger)
    {
        Logger = logger;
    }

    public abstract void ConfigureKernel(IKernelBuilder builder);

    public abstract Task<IReadOnlyList<AIModel>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default);

    public virtual Task<AIModelDetails?> GetModelDetailsAsync(
        string modelId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AIModelDetails?>(null);
    }

    public virtual object? GetNativeClient() => null;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                DisposeResources();
            }
            _disposed = true;
        }
    }

    protected virtual void DisposeResources() { }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
