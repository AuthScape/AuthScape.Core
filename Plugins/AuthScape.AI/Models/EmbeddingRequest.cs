using AuthScape.AI.Enums;

namespace AuthScape.AI.Models;

public class EmbeddingRequest
{
    public required IList<string> Texts { get; init; }
    public string? ModelId { get; set; }
    public AIProvider? Provider { get; set; }
}
