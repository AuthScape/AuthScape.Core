using AuthScape.AI.Enums;
using Microsoft.Extensions.AI;

namespace AuthScape.AI.Models;

public class ChatResponse
{
    public string Text { get; init; } = string.Empty;
    public ChatRole Role { get; init; } = ChatRole.Assistant;
    public AIProvider Provider { get; init; }
    public string? ModelId { get; init; }
    public IReadOnlyList<ToolCallInfo>? ToolCalls { get; init; }
    public UsageInfo? Usage { get; init; }
    public string? FinishReason { get; init; }

    /// <summary>
    /// The raw Microsoft.Extensions.AI ChatResponse for advanced scenarios.
    /// </summary>
    public Microsoft.Extensions.AI.ChatResponse? RawResponse { get; init; }
}
