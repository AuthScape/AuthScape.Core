using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

namespace AuthScape.AI.Memory;

/// <summary>
/// Memory/RAG service backed by SK's InMemoryVectorStore and IEmbeddingGenerator.
/// </summary>
public class AIMemoryService : IAIMemoryService
{
    private readonly InMemoryVectorStore _vectorStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public AIMemoryService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _vectorStore = new InMemoryVectorStore();
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task RememberAsync(string collection, string key, string text,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = await _embeddingGenerator.GenerateAsync(
            [text], cancellationToken: cancellationToken);
        var embedding = embeddings[0].Vector;

        var recordCollection = _vectorStore.GetCollection<string, MemoryRecord>(collection);
        await recordCollection.EnsureCollectionExistsAsync(cancellationToken);

        var record = new MemoryRecord
        {
            Key = key,
            Text = text,
            Collection = collection,
            Embedding = embedding,
            Metadata = metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        await recordCollection.UpsertAsync(record, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryResult>> RecallAsync(string collection, string query,
        int limit = 5, float minRelevance = 0.5f,
        CancellationToken cancellationToken = default)
    {
        var embeddings = await _embeddingGenerator.GenerateAsync(
            [query], cancellationToken: cancellationToken);
        var queryEmbedding = embeddings[0].Vector;

        var recordCollection = _vectorStore.GetCollection<string, MemoryRecord>(collection);

        var searchResults = recordCollection.SearchAsync(
            queryEmbedding,
            limit,
            cancellationToken: cancellationToken);

        var results = new List<MemoryResult>();
        await foreach (var result in searchResults)
        {
            var score = (float)(result.Score ?? 0);
            if (score >= minRelevance && result.Record is not null)
            {
                results.Add(new MemoryResult(
                    result.Record.Key,
                    result.Record.Text,
                    score,
                    result.Record.Metadata));
            }
        }

        return results.AsReadOnly();
    }

    public async Task<bool> ForgetAsync(string collection, string key,
        CancellationToken cancellationToken = default)
    {
        var recordCollection = _vectorStore.GetCollection<string, MemoryRecord>(collection);
        await recordCollection.DeleteAsync(key, cancellationToken: cancellationToken);
        return true;
    }
}
