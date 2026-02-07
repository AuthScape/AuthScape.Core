using AuthScape.AI.Enums;
using AuthScape.AI.Memory;
using AuthScape.AI.Models;
using AuthScape.AI.Tools;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace AuthScape.AI;

/// <summary>
/// Primary facade for all AI operations. Inject this interface to interact
/// with any configured AI provider through a unified API.
/// </summary>
public interface IAIService
{
    // ── Provider Management ──────────────────────────────────────────

    /// <summary>Gets the currently active provider.</summary>
    AIProvider ActiveProvider { get; }

    /// <summary>Switches the active provider at runtime.</summary>
    void SetProvider(AIProvider provider);

    /// <summary>Gets all registered and configured providers.</summary>
    IReadOnlyList<AIProvider> GetConfiguredProviders();

    /// <summary>Checks whether a provider supports a given capability.</summary>
    bool SupportsCapability(AIProvider provider, AICapability capability);

    // ── Model Discovery ──────────────────────────────────────────────

    /// <summary>Gets all available models from the active provider.</summary>
    Task<IReadOnlyList<AIModel>> GetAvailableModels(CancellationToken cancellationToken = default);

    /// <summary>Gets all available models from a specific provider.</summary>
    Task<IReadOnlyList<AIModel>> GetAvailableModels(AIProvider provider, CancellationToken cancellationToken = default);

    /// <summary>Gets detailed information about a specific model.</summary>
    Task<AIModelDetails?> GetModelDetails(string modelId, AIProvider? provider = null, CancellationToken cancellationToken = default);

    // ── Chat ─────────────────────────────────────────────────────────

    /// <summary>Sends a simple text prompt and returns the response.</summary>
    Task<Models.ChatResponse> ChatAsync(string prompt, string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default);

    /// <summary>Sends a conversation (list of messages) and returns the response.</summary>
    Task<Models.ChatResponse> ChatAsync(IEnumerable<ChatMessage> messages, ChatRequest? options = null, CancellationToken cancellationToken = default);

    // ── Streaming Chat ───────────────────────────────────────────────

    /// <summary>Sends a simple text prompt and streams the response.</summary>
    IAsyncEnumerable<StreamingChatUpdate> ChatStreamAsync(string prompt, string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default);

    /// <summary>Sends a conversation and streams the response.</summary>
    IAsyncEnumerable<StreamingChatUpdate> ChatStreamAsync(IEnumerable<ChatMessage> messages, ChatRequest? options = null, CancellationToken cancellationToken = default);

    // ── Embeddings ───────────────────────────────────────────────────

    /// <summary>Generates an embedding vector for a single text input.</summary>
    Task<ReadOnlyMemory<float>> EmbedAsync(string text, string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default);

    /// <summary>Generates embedding vectors for multiple text inputs.</summary>
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(IEnumerable<string> texts, string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default);

    // ── Vision ───────────────────────────────────────────────────────

    /// <summary>Sends a text prompt with image data and returns the response.</summary>
    Task<Models.ChatResponse> ChatWithVisionAsync(string prompt, byte[] imageData, string mediaType = "image/png", string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default);

    /// <summary>Sends a text prompt with an image URL and returns the response.</summary>
    Task<Models.ChatResponse> ChatWithVisionAsync(string prompt, Uri imageUrl, string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default);

    // ── Tool Calling ─────────────────────────────────────────────────

    /// <summary>Sends a chat with registered tools available for the model to call.</summary>
    Task<Models.ChatResponse> ChatWithToolsAsync(IEnumerable<ChatMessage> messages, IEnumerable<IAITool>? tools = null, bool autoInvoke = true, ChatRequest? options = null, CancellationToken cancellationToken = default);

    /// <summary>Sends a chat with registered tools and streams the response.</summary>
    IAsyncEnumerable<StreamingChatUpdate> ChatWithToolsStreamAsync(IEnumerable<ChatMessage> messages, IEnumerable<IAITool>? tools = null, bool autoInvoke = true, ChatRequest? options = null, CancellationToken cancellationToken = default);

    // ── Model Management (Ollama-centric) ────────────────────────────

    /// <summary>Pulls/downloads a model. Supported by Ollama.</summary>
    IAsyncEnumerable<ModelPullProgress> PullModelAsync(string modelName, AIProvider? provider = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a local model. Supported by Ollama.</summary>
    Task<bool> DeleteModelAsync(string modelName, AIProvider? provider = null, CancellationToken cancellationToken = default);

    // ── Low-Level Access ─────────────────────────────────────────────

    /// <summary>Gets the underlying IChatClient for the active or specified provider.</summary>
    IChatClient GetChatClient(AIProvider? provider = null);

    /// <summary>Gets the underlying IEmbeddingGenerator for the active or specified provider.</summary>
    IEmbeddingGenerator<string, Embedding<float>>? GetEmbeddingGenerator(AIProvider? provider = null);

    /// <summary>Gets the underlying provider-specific client.</summary>
    T? GetNativeClient<T>(AIProvider? provider = null) where T : class;

    // ── Semantic Kernel ───────────────────────────────────────────────

    /// <summary>Gets the underlying SK Kernel for the active or specified provider.</summary>
    Kernel GetKernel(AIProvider? provider = null);

    // ── Memory / RAG ──────────────────────────────────────────────────

    /// <summary>Gets the memory/RAG service, if enabled via WithMemory().</summary>
    IAIMemoryService? Memory { get; }

    // ── Tool Registry ────────────────────────────────────────────────

    /// <summary>Gets the tool registry for managing tools.</summary>
    IAIToolRegistry ToolRegistry { get; }
}
