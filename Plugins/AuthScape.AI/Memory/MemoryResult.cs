namespace AuthScape.AI.Memory;

/// <summary>
/// A single result from a semantic memory search.
/// </summary>
public record MemoryResult(
    string Key,
    string Text,
    float Relevance,
    IDictionary<string, string>? Metadata = null);
