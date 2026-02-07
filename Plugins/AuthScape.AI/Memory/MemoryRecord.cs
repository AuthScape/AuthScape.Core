using Microsoft.Extensions.VectorData;

namespace AuthScape.AI.Memory;

/// <summary>
/// Internal record stored in the vector store.
/// </summary>
internal class MemoryRecord
{
    [VectorStoreKey]
    public string Key { get; set; } = string.Empty;

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    [VectorStoreData]
    public string Collection { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }

    [VectorStoreData]
    public Dictionary<string, string>? Metadata { get; set; }
}
