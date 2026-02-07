using Microsoft.Extensions.AI;

namespace AuthScape.AI.Extensions;

public static class ChatMessageExtensions
{
    /// <summary>
    /// Creates a user message with text content.
    /// </summary>
    public static ChatMessage User(string text) =>
        new(ChatRole.User, text);

    /// <summary>
    /// Creates a system message.
    /// </summary>
    public static ChatMessage System(string text) =>
        new(ChatRole.System, text);

    /// <summary>
    /// Creates an assistant message.
    /// </summary>
    public static ChatMessage Assistant(string text) =>
        new(ChatRole.Assistant, text);

    /// <summary>
    /// Creates a user message with text and an image from bytes.
    /// </summary>
    public static ChatMessage UserWithImage(string text, byte[] imageData, string mediaType = "image/png") =>
        new(ChatRole.User, [
            new TextContent(text),
            new DataContent(imageData, mediaType)
        ]);

    /// <summary>
    /// Creates a user message with text and an image from a URL.
    /// </summary>
    public static ChatMessage UserWithImage(string text, Uri imageUrl) =>
        new(ChatRole.User, [
            new TextContent(text),
            new UriContent(imageUrl, "image/*")
        ]);

    /// <summary>
    /// Builds a conversation from a system prompt and user message.
    /// </summary>
    public static List<ChatMessage> BuildConversation(string systemPrompt, string userMessage) =>
        [System(systemPrompt), User(userMessage)];
}
