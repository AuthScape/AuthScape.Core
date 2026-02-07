namespace AuthScape.AI.Memory;

/// <summary>
/// Simple memory/RAG abstraction. Remember facts, recall relevant ones by semantic search.
/// </summary>
public interface IAIMemoryService
{
    /// <summary>Stores text with an embedding for later semantic retrieval.</summary>
    Task RememberAsync(string collection, string key, string text,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Searches stored memories by semantic similarity.</summary>
    Task<IReadOnlyList<MemoryResult>> RecallAsync(string collection, string query,
        int limit = 5, float minRelevance = 0.5f,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a specific memory by key.</summary>
    Task<bool> ForgetAsync(string collection, string key,
        CancellationToken cancellationToken = default);
}
