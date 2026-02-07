namespace AuthScape.AI.Models;

public record ModelPullProgress
{
    public string Status { get; init; } = string.Empty;
    public double? PercentComplete { get; init; }
    public long? BytesCompleted { get; init; }
    public long? BytesTotal { get; init; }
}
