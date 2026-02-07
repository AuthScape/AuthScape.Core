using AuthScape.AI.Enums;
using AuthScape.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AuthScape.AI.Mappers;

internal static class ChatResponseMapper
{
    // ── M.E.AI Mappers (kept for backward compat) ────────────────────

    public static Models.ChatResponse FromMEAI(
        Microsoft.Extensions.AI.ChatResponse response,
        AIProvider provider)
    {
        var message = response.Messages.LastOrDefault();
        var text = message?.Text ?? string.Empty;

        var toolCalls = message?.Contents
            .OfType<Microsoft.Extensions.AI.FunctionCallContent>()
            .Select(fc => new ToolCallInfo(
                fc.Name ?? "",
                System.Text.Json.JsonSerializer.Serialize(fc.Arguments),
                null))
            .ToList() ?? [];

        UsageInfo? usage = null;
        if (response.Usage is { } u)
        {
            usage = new UsageInfo(u.InputTokenCount, u.OutputTokenCount, u.TotalTokenCount);
        }

        return new Models.ChatResponse
        {
            Text = text,
            Role = message?.Role ?? ChatRole.Assistant,
            Provider = provider,
            ModelId = response.ModelId,
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null,
            Usage = usage,
            FinishReason = response.FinishReason?.ToString(),
            RawResponse = response
        };
    }

    public static StreamingChatUpdate FromMEAIUpdate(
        ChatResponseUpdate update,
        AIProvider provider,
        bool isComplete = false)
    {
        return new StreamingChatUpdate
        {
            Text = update.Text,
            Role = update.Role,
            Provider = provider,
            ModelId = update.ModelId,
            IsComplete = isComplete
        };
    }

    // ── SK Mappers ───────────────────────────────────────────────────

    public static Models.ChatResponse FromSK(
        ChatMessageContent content,
        AIProvider provider)
    {
        var text = content.Content ?? string.Empty;

        var toolCalls = content.Items
            .OfType<Microsoft.SemanticKernel.FunctionCallContent>()
            .Select(fc => new ToolCallInfo(
                fc.FunctionName ?? "",
                fc.Arguments is not null
                    ? System.Text.Json.JsonSerializer.Serialize(fc.Arguments)
                    : "{}",
                fc.Id))
            .ToList();

        UsageInfo? usage = null;
        if (content.Metadata is not null)
        {
            int? inputTokens = null, outputTokens = null, totalTokens = null;

            if (content.Metadata.TryGetValue("Usage", out var usageObj) && usageObj is not null)
            {
                var usageType = usageObj.GetType();
                inputTokens = GetPropertyValue<int?>(usageObj, usageType, "InputTokenCount")
                    ?? GetPropertyValue<int?>(usageObj, usageType, "PromptTokens");
                outputTokens = GetPropertyValue<int?>(usageObj, usageType, "OutputTokenCount")
                    ?? GetPropertyValue<int?>(usageObj, usageType, "CompletionTokens");
                totalTokens = GetPropertyValue<int?>(usageObj, usageType, "TotalTokenCount")
                    ?? GetPropertyValue<int?>(usageObj, usageType, "TotalTokens");
            }

            if (inputTokens.HasValue || outputTokens.HasValue)
            {
                usage = new UsageInfo(inputTokens, outputTokens,
                    totalTokens ?? (inputTokens ?? 0) + (outputTokens ?? 0));
            }
        }

        string? finishReason = null;
        if (content.Metadata?.TryGetValue("FinishReason", out var fr) == true)
            finishReason = fr?.ToString();

        return new Models.ChatResponse
        {
            Text = text,
            Role = MapRole(content.Role),
            Provider = provider,
            ModelId = content.ModelId,
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null,
            Usage = usage,
            FinishReason = finishReason,
            RawResponse = null
        };
    }

    public static StreamingChatUpdate FromSKStreaming(
        StreamingChatMessageContent content,
        AIProvider provider)
    {
        return new StreamingChatUpdate
        {
            Text = content.Content,
            Role = content.Role is { } role ? MapRole(role) : null,
            Provider = provider,
            ModelId = content.ModelId,
            IsComplete = false
        };
    }

    // ── Conversion helpers ───────────────────────────────────────────

    public static ChatHistory ToChatHistory(IEnumerable<ChatMessage> messages)
    {
        var history = new ChatHistory();

        foreach (var msg in messages)
        {
            var role = ToAuthorRole(msg.Role);
            var hasNonText = msg.Contents.Any(c => c is not Microsoft.Extensions.AI.TextContent);

            if (hasNonText)
            {
                var items = new ChatMessageContentItemCollection();
                foreach (var content in msg.Contents)
                {
                    switch (content)
                    {
                        case Microsoft.Extensions.AI.TextContent tc:
                            items.Add(new Microsoft.SemanticKernel.TextContent(tc.Text));
                            break;
                        case DataContent dc:
                            items.Add(new ImageContent(dc.Data, dc.MediaType));
                            break;
                        case UriContent uc:
                            items.Add(new ImageContent(uc.Uri));
                            break;
                    }
                }
                history.Add(new ChatMessageContent(role, items));
            }
            else
            {
                history.AddMessage(role, msg.Text ?? string.Empty);
            }
        }

        return history;
    }

    public static PromptExecutionSettings? ToPromptSettings(Models.ChatRequest? request)
    {
        if (request is null) return null;

        var settings = new PromptExecutionSettings
        {
            ModelId = request.ModelId,
        };

        var ext = new Dictionary<string, object>();
        if (request.Temperature.HasValue) ext["temperature"] = request.Temperature.Value;
        if (request.MaxTokens.HasValue) ext["max_tokens"] = request.MaxTokens.Value;
        if (request.TopP.HasValue) ext["top_p"] = request.TopP.Value;
        if (request.StopSequences?.Count > 0) ext["stop"] = request.StopSequences;

        if (ext.Count > 0)
            settings.ExtensionData = ext;

        return settings;
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static ChatRole MapRole(AuthorRole role) =>
        role == AuthorRole.User ? ChatRole.User
        : role == AuthorRole.System ? ChatRole.System
        : role == AuthorRole.Tool ? ChatRole.Tool
        : ChatRole.Assistant;

    private static AuthorRole ToAuthorRole(ChatRole role) =>
        role == ChatRole.User ? AuthorRole.User
        : role == ChatRole.System ? AuthorRole.System
        : role == ChatRole.Tool ? AuthorRole.Tool
        : AuthorRole.Assistant;

    private static T? GetPropertyValue<T>(object obj, Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName);
        if (prop is null) return default;
        var value = prop.GetValue(obj);
        if (value is T t) return t;
        if (value is not null)
        {
            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch { return default; }
        }
        return default;
    }
}
