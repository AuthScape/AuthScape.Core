namespace AuthScape.AI.Models;

public record AIModelDetails : AIModel
{
    public string? Family { get; init; }
    public string? QuantizationLevel { get; init; }
    public long? SizeBytes { get; init; }
    public string? License { get; init; }
    public string? SystemPrompt { get; init; }
    public string? Template { get; init; }
    public IReadOnlyDictionary<string, object>? Parameters { get; init; }
}
