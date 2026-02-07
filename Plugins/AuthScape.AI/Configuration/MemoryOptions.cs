using AuthScape.AI.Enums;

namespace AuthScape.AI.Configuration;

public class MemoryOptions
{
    public AIProvider? EmbeddingProvider { get; set; }
    public string? EmbeddingModel { get; set; }
    public string DefaultCollection { get; set; } = "default";
}
