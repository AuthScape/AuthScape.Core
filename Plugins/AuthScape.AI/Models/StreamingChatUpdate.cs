using AuthScape.AI.Enums;
using Microsoft.Extensions.AI;

namespace AuthScape.AI.Models;

public class StreamingChatUpdate
{
    public string? Text { get; init; }
    public ChatRole? Role { get; init; }
    public AIProvider Provider { get; init; }
    public string? ModelId { get; init; }
    public bool IsComplete { get; init; }

    public override string ToString() => Text ?? string.Empty;
}
