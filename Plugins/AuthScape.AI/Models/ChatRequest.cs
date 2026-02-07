using AuthScape.AI.Enums;

namespace AuthScape.AI.Models;

public class ChatRequest
{
    public string? ModelId { get; set; }
    public AIProvider? Provider { get; set; }
    public float? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public float? TopP { get; set; }
    public string? SystemPrompt { get; set; }
    public IList<string>? StopSequences { get; set; }
    public bool? EnableToolCalling { get; set; }
    public IList<string>? ToolNames { get; set; }
    public bool AutoInvokeTools { get; set; } = true;
    public IDictionary<string, object>? AdditionalProperties { get; set; }
}
