namespace AuthScape.AI.Models;

public record UsageInfo(long? PromptTokens, long? CompletionTokens, long? TotalTokens);
