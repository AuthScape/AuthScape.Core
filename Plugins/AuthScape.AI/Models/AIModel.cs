using AuthScape.AI.Enums;

namespace AuthScape.AI.Models;

public record AIModel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required AIProvider Provider { get; init; }
    public AICapability Capabilities { get; init; } = AICapability.Chat;
    public long? ParameterCount { get; init; }
    public DateTime? ModifiedAt { get; init; }
}
