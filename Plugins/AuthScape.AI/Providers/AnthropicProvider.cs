using Anthropic;
using AuthScape.AI.Configuration;
using AuthScape.AI.Enums;
using AuthScape.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AuthScape.AI.Providers;

public class AnthropicProvider : AIProviderBase
{
    private readonly AnthropicOptions _options;
    private readonly AnthropicClient? _client;

    public override AIProvider ProviderType => AIProvider.Anthropic;

    public override ProviderCapabilities Capabilities => new()
    {
        SupportedCapabilities = AICapability.Chat | AICapability.Streaming
            | AICapability.Vision | AICapability.ToolCalling
    };

    public override bool IsConfigured => !string.IsNullOrEmpty(_options.ApiKey);

    public AnthropicProvider(IOptions<AIServiceOptions> options, ILogger<AnthropicProvider> logger)
        : base(logger)
    {
        _options = options.Value.Anthropic ?? new AnthropicOptions();
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _client = new AnthropicClient(new Anthropic.Core.ClientOptions
            {
                ApiKey = _options.ApiKey
            });
        }
    }

    public override void ConfigureKernel(IKernelBuilder builder)
    {
        if (_client is null) return;

        var modelId = _options.DefaultModel ?? "claude-sonnet-4-5-20250514";
        // Bridge: Anthropic SDK → IChatClient → SK IChatCompletionService
        var chatClient = _client.AsIChatClient(modelId, _options.MaxTokensDefault);
        builder.Services.AddSingleton<IChatCompletionService>(
            chatClient.AsChatCompletionService());
    }

    public override Task<IReadOnlyList<AIModel>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var models = new List<AIModel>
        {
            CreateModel("claude-opus-4-20250514", "Claude Opus 4"),
            CreateModel("claude-sonnet-4-5-20250514", "Claude Sonnet 4.5"),
            CreateModel("claude-sonnet-4-20250514", "Claude Sonnet 4"),
            CreateModel("claude-haiku-3-5-20241022", "Claude 3.5 Haiku"),
        };

        return Task.FromResult<IReadOnlyList<AIModel>>(models.AsReadOnly());
    }

    private static AIModel CreateModel(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Provider = AIProvider.Anthropic,
        Capabilities = AICapability.Chat | AICapability.Streaming | AICapability.Vision | AICapability.ToolCalling
    };

    public override object? GetNativeClient() => _client;
}
