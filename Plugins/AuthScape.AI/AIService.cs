using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AuthScape.AI.Enums;
using AuthScape.AI.Exceptions;
using AuthScape.AI.Mappers;
using AuthScape.AI.Memory;
using AuthScape.AI.Models;
using AuthScape.AI.Providers;
using AuthScape.AI.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AuthScape.AI;

/// <summary>
/// Default implementation of IAIService. Routes requests through per-provider
/// Semantic Kernel instances, with shared tool plugins and optional memory.
/// </summary>
public class AIService : IAIService
{
    private readonly Dictionary<AIProvider, Kernel> _kernels;
    private readonly Dictionary<AIProvider, IAIProvider> _providers;
    private readonly IAIToolRegistry _toolRegistry;
    private readonly ILogger<AIService> _logger;
    private readonly IAIMemoryService? _memory;
    private AIProvider _activeProvider;

    public AIProvider ActiveProvider => _activeProvider;
    public IAIToolRegistry ToolRegistry => _toolRegistry;
    public IAIMemoryService? Memory => _memory;

    public AIService(
        IEnumerable<IAIProvider> providers,
        IAIToolRegistry toolRegistry,
        ILogger<AIService> logger,
        AIProvider defaultProvider = AIProvider.Ollama,
        IEnumerable<KernelPlugin>? additionalPlugins = null,
        IAIMemoryService? memory = null)
    {
        _toolRegistry = toolRegistry;
        _logger = logger;
        _memory = memory;

        _providers = providers
            .Where(p => p.IsConfigured)
            .ToDictionary(p => p.ProviderType);

        // Build a shared KernelPlugin from the tool registry
        KernelPlugin? toolPlugin = null;
        if (toolRegistry.GetAllTools().Count > 0)
            toolPlugin = toolRegistry.ToKernelPlugin();

        // Build a Kernel for each configured provider
        _kernels = new Dictionary<AIProvider, Kernel>();
        foreach (var (type, provider) in _providers)
        {
            var builder = Kernel.CreateBuilder();
            provider.ConfigureKernel(builder);

            if (toolPlugin is not null)
                builder.Plugins.Add(toolPlugin);

            if (additionalPlugins is not null)
            {
                foreach (var plugin in additionalPlugins)
                    builder.Plugins.Add(plugin);
            }

            _kernels[type] = builder.Build();
        }

        _activeProvider = _kernels.ContainsKey(defaultProvider)
            ? defaultProvider
            : _kernels.Keys.FirstOrDefault();
    }

    // ── Provider Management ──────────────────────────────────────────

    public void SetProvider(AIProvider provider)
    {
        if (!_providers.ContainsKey(provider))
            throw new ProviderNotConfiguredException(provider);
        _activeProvider = provider;
    }

    public IReadOnlyList<AIProvider> GetConfiguredProviders() =>
        _providers.Keys.ToList().AsReadOnly();

    public bool SupportsCapability(AIProvider provider, AICapability capability)
    {
        if (!_providers.TryGetValue(provider, out var p)) return false;
        return p.Capabilities.Supports(capability);
    }

    // ── Model Discovery ──────────────────────────────────────────────

    public Task<IReadOnlyList<AIModel>> GetAvailableModels(CancellationToken cancellationToken = default) =>
        GetAvailableModels(_activeProvider, cancellationToken);

    public async Task<IReadOnlyList<AIModel>> GetAvailableModels(AIProvider provider, CancellationToken cancellationToken = default)
    {
        var p = ResolveProvider(provider);
        return await WrapAsync(() => p.GetAvailableModelsAsync(cancellationToken), provider);
    }

    public async Task<AIModelDetails?> GetModelDetails(string modelId, AIProvider? provider = null, CancellationToken cancellationToken = default)
    {
        var p = ResolveProvider(provider);
        return await WrapAsync(() => p.GetModelDetailsAsync(modelId, cancellationToken), p.ProviderType);
    }

    // ── Chat ─────────────────────────────────────────────────────────

    public async Task<Models.ChatResponse> ChatAsync(string prompt, string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default)
    {
        var providerType = ResolveProviderType(provider);
        var kernel = ResolveKernel(provider);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var settings = modelId is not null
            ? new PromptExecutionSettings { ModelId = modelId }
            : null;

        return await WrapAsync(async () =>
        {
            var result = await chatService.GetChatMessageContentAsync(
                history, settings, kernel, cancellationToken);
            return ChatResponseMapper.FromSK(result, providerType);
        }, providerType);
    }

    public async Task<Models.ChatResponse> ChatAsync(IEnumerable<ChatMessage> messages, ChatRequest? options = null, CancellationToken cancellationToken = default)
    {
        var providerType = ResolveProviderType(options?.Provider);
        var kernel = ResolveKernel(options?.Provider);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = ChatResponseMapper.ToChatHistory(messages);

        // Add system prompt if provided
        if (!string.IsNullOrEmpty(options?.SystemPrompt))
            history.Insert(0, new ChatMessageContent(AuthorRole.System, options.SystemPrompt));

        var settings = ChatResponseMapper.ToPromptSettings(options);

        return await WrapAsync(async () =>
        {
            var result = await chatService.GetChatMessageContentAsync(
                history, settings, kernel, cancellationToken);
            return ChatResponseMapper.FromSK(result, providerType);
        }, providerType);
    }

    // ── Streaming Chat ───────────────────────────────────────────────

    public async IAsyncEnumerable<StreamingChatUpdate> ChatStreamAsync(
        string prompt,
        string? modelId = null,
        AIProvider? provider = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var providerType = ResolveProviderType(provider);
        var kernel = ResolveKernel(provider);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var settings = modelId is not null
            ? new PromptExecutionSettings { ModelId = modelId }
            : null;

        await foreach (var content in chatService.GetStreamingChatMessageContentsAsync(
            history, settings, kernel, cancellationToken))
        {
            yield return ChatResponseMapper.FromSKStreaming(content, providerType);
        }
    }

    public async IAsyncEnumerable<StreamingChatUpdate> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatRequest? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var providerType = ResolveProviderType(options?.Provider);
        var kernel = ResolveKernel(options?.Provider);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = ChatResponseMapper.ToChatHistory(messages);

        if (!string.IsNullOrEmpty(options?.SystemPrompt))
            history.Insert(0, new ChatMessageContent(AuthorRole.System, options.SystemPrompt));

        var settings = ChatResponseMapper.ToPromptSettings(options);

        await foreach (var content in chatService.GetStreamingChatMessageContentsAsync(
            history, settings, kernel, cancellationToken))
        {
            yield return ChatResponseMapper.FromSKStreaming(content, providerType);
        }
    }

    // ── Embeddings ───────────────────────────────────────────────────

    public async Task<ReadOnlyMemory<float>> EmbedAsync(string text, string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default)
    {
        var providerType = ResolveProviderType(provider);
        var kernel = ResolveKernel(provider);
        var generator = kernel.Services.GetService(typeof(IEmbeddingGenerator<string, Embedding<float>>))
            as IEmbeddingGenerator<string, Embedding<float>>
            ?? throw new CapabilityNotSupportedException(providerType, AICapability.Embeddings);

        return await WrapAsync(async () =>
        {
            var result = await generator.GenerateAsync(
                [text], cancellationToken: cancellationToken);
            return result[0].Vector;
        }, providerType);
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(IEnumerable<string> texts, string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default)
    {
        var providerType = ResolveProviderType(provider);
        var kernel = ResolveKernel(provider);
        var generator = kernel.Services.GetService(typeof(IEmbeddingGenerator<string, Embedding<float>>))
            as IEmbeddingGenerator<string, Embedding<float>>
            ?? throw new CapabilityNotSupportedException(providerType, AICapability.Embeddings);

        return await WrapAsync(async () =>
        {
            var result = await generator.GenerateAsync(
                texts.ToList(), cancellationToken: cancellationToken);
            return (IReadOnlyList<ReadOnlyMemory<float>>)result.Select(e => e.Vector).ToList().AsReadOnly();
        }, providerType);
    }

    // ── Vision ───────────────────────────────────────────────────────

    public async Task<Models.ChatResponse> ChatWithVisionAsync(string prompt, byte[] imageData, string mediaType = "image/png", string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default)
    {
        var providerType = ResolveProviderType(provider);
        if (!_providers[providerType].Capabilities.Supports(AICapability.Vision))
            throw new CapabilityNotSupportedException(providerType, AICapability.Vision);

        var kernel = ResolveKernel(provider);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.Add(new ChatMessageContent(AuthorRole.User,
            new ChatMessageContentItemCollection
            {
                new Microsoft.SemanticKernel.TextContent(prompt),
                new ImageContent(imageData, mediaType)
            }));

        var settings = modelId is not null
            ? new PromptExecutionSettings { ModelId = modelId }
            : null;

        return await WrapAsync(async () =>
        {
            var result = await chatService.GetChatMessageContentAsync(
                history, settings, kernel, cancellationToken);
            return ChatResponseMapper.FromSK(result, providerType);
        }, providerType);
    }

    public async Task<Models.ChatResponse> ChatWithVisionAsync(string prompt, Uri imageUrl, string? modelId = null, AIProvider? provider = null, CancellationToken cancellationToken = default)
    {
        var providerType = ResolveProviderType(provider);
        if (!_providers[providerType].Capabilities.Supports(AICapability.Vision))
            throw new CapabilityNotSupportedException(providerType, AICapability.Vision);

        var kernel = ResolveKernel(provider);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.Add(new ChatMessageContent(AuthorRole.User,
            new ChatMessageContentItemCollection
            {
                new Microsoft.SemanticKernel.TextContent(prompt),
                new ImageContent(imageUrl)
            }));

        var settings = modelId is not null
            ? new PromptExecutionSettings { ModelId = modelId }
            : null;

        return await WrapAsync(async () =>
        {
            var result = await chatService.GetChatMessageContentAsync(
                history, settings, kernel, cancellationToken);
            return ChatResponseMapper.FromSK(result, providerType);
        }, providerType);
    }

    // ── Tool Calling ─────────────────────────────────────────────────

    public async Task<Models.ChatResponse> ChatWithToolsAsync(
        IEnumerable<ChatMessage> messages,
        IEnumerable<IAITool>? tools = null,
        bool autoInvoke = true,
        ChatRequest? options = null,
        CancellationToken cancellationToken = default)
    {
        var providerType = ResolveProviderType(options?.Provider);
        var kernel = ResolveKernel(options?.Provider);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = ChatResponseMapper.ToChatHistory(messages);

        if (!string.IsNullOrEmpty(options?.SystemPrompt))
            history.Insert(0, new ChatMessageContent(AuthorRole.System, options.SystemPrompt));

        var settings = ChatResponseMapper.ToPromptSettings(options) ?? new PromptExecutionSettings();
        settings.FunctionChoiceBehavior = autoInvoke
            ? FunctionChoiceBehavior.Auto()
            : FunctionChoiceBehavior.Auto(autoInvoke: false);

        return await WrapAsync(async () =>
        {
            var result = await chatService.GetChatMessageContentAsync(
                history, settings, kernel, cancellationToken);
            return ChatResponseMapper.FromSK(result, providerType);
        }, providerType);
    }

    public async IAsyncEnumerable<StreamingChatUpdate> ChatWithToolsStreamAsync(
        IEnumerable<ChatMessage> messages,
        IEnumerable<IAITool>? tools = null,
        bool autoInvoke = true,
        ChatRequest? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var providerType = ResolveProviderType(options?.Provider);
        var kernel = ResolveKernel(options?.Provider);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = ChatResponseMapper.ToChatHistory(messages);

        if (!string.IsNullOrEmpty(options?.SystemPrompt))
            history.Insert(0, new ChatMessageContent(AuthorRole.System, options.SystemPrompt));

        var settings = ChatResponseMapper.ToPromptSettings(options) ?? new PromptExecutionSettings();
        settings.FunctionChoiceBehavior = autoInvoke
            ? FunctionChoiceBehavior.Auto()
            : FunctionChoiceBehavior.Auto(autoInvoke: false);

        await foreach (var content in chatService.GetStreamingChatMessageContentsAsync(
            history, settings, kernel, cancellationToken))
        {
            yield return ChatResponseMapper.FromSKStreaming(content, providerType);
        }
    }

    // ── Model Management ─────────────────────────────────────────────

    public async IAsyncEnumerable<ModelPullProgress> PullModelAsync(
        string modelName,
        AIProvider? provider = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var p = ResolveProvider(provider);
        if (p is not IModelManager manager)
            throw new CapabilityNotSupportedException(p.ProviderType, AICapability.ModelManagement);

        await foreach (var progress in manager.PullModelAsync(modelName, cancellationToken))
        {
            yield return progress;
        }
    }

    public async Task<bool> DeleteModelAsync(string modelName, AIProvider? provider = null, CancellationToken cancellationToken = default)
    {
        var p = ResolveProvider(provider);
        if (p is not IModelManager manager)
            throw new CapabilityNotSupportedException(p.ProviderType, AICapability.ModelManagement);

        return await manager.DeleteModelAsync(modelName, cancellationToken);
    }

    // ── Low-Level Access ─────────────────────────────────────────────

    public Kernel GetKernel(AIProvider? provider = null) =>
        ResolveKernel(provider);

    public IChatClient GetChatClient(AIProvider? provider = null)
    {
        var kernel = ResolveKernel(provider);

        // Try to get IChatClient directly from kernel services
        var chatClient = kernel.Services.GetService(typeof(IChatClient)) as IChatClient;
        if (chatClient is not null) return chatClient;

        // Try casting IChatCompletionService (newer SK versions may implement both)
        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        if (chatCompletion is IChatClient client) return client;

        throw new InvalidOperationException(
            "IChatClient is not available for this provider. Use GetKernel() for direct Semantic Kernel access.");
    }

    public IEmbeddingGenerator<string, Embedding<float>>? GetEmbeddingGenerator(AIProvider? provider = null)
    {
        var kernel = ResolveKernel(provider);
        return kernel.Services.GetService(typeof(IEmbeddingGenerator<string, Embedding<float>>))
            as IEmbeddingGenerator<string, Embedding<float>>;
    }

    public T? GetNativeClient<T>(AIProvider? provider = null) where T : class =>
        ResolveProvider(provider).GetNativeClient() as T;

    // ── Private Helpers ──────────────────────────────────────────────

    private IAIProvider ResolveProvider(AIProvider? provider = null)
    {
        var target = provider ?? _activeProvider;
        if (!_providers.TryGetValue(target, out var resolved))
            throw new ProviderNotConfiguredException(target);
        return resolved;
    }

    private AIProvider ResolveProviderType(AIProvider? provider = null) =>
        provider ?? _activeProvider;

    private Kernel ResolveKernel(AIProvider? provider = null)
    {
        var target = provider ?? _activeProvider;
        if (!_kernels.TryGetValue(target, out var kernel))
            throw new ProviderNotConfiguredException(target);
        return kernel;
    }

    private async Task<T> WrapAsync<T>(Func<Task<T>> action, AIProvider provider)
    {
        try
        {
            return await action();
        }
        catch (AIServiceException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new AIRateLimitException(provider, ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new AIAuthenticationException(provider, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI request to {Provider} failed", provider);
            throw new AIServiceException($"Request to {provider} failed: {ex.Message}", ex);
        }
    }
}
