using AuthScape.AI.Models;

namespace AuthScape.AI.Providers;

/// <summary>
/// Optional interface for providers that support model lifecycle management (e.g., Ollama).
/// </summary>
public interface IModelManager
{
    IAsyncEnumerable<ModelPullProgress> PullModelAsync(
        string modelName, CancellationToken cancellationToken = default);

    Task<bool> DeleteModelAsync(
        string modelName, CancellationToken cancellationToken = default);

    Task<bool> CopyModelAsync(
        string source, string destination, CancellationToken cancellationToken = default);
}
