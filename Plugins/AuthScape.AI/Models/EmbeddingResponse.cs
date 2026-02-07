using AuthScape.AI.Enums;

namespace AuthScape.AI.Models;

public class EmbeddingResponse
{
    public required IReadOnlyList<ReadOnlyMemory<float>> Vectors { get; init; }
    public string? ModelId { get; init; }
    public AIProvider Provider { get; init; }
    public UsageInfo? Usage { get; init; }
}
