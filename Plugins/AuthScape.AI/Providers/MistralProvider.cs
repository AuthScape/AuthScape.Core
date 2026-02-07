using AuthScape.AI.Configuration;
using AuthScape.AI.Enums;
using AuthScape.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace AuthScape.AI.Providers;

public class MistralProvider : AIProviderBase
{
    private readonly MistralOptions _options;

    public override AIProvider ProviderType => AIProvider.Mistral;

    public override ProviderCapabilities Capabilities => new()
    {
        SupportedCapabilities = AICapability.Chat | AICapability.Streaming
            | AICapability.Embeddings | AICapability.ToolCalling
    };

    public override bool IsConfigured => !string.IsNullOrEmpty(_options.ApiKey);

    public MistralProvider(IOptions<AIServiceOptions> options, ILogger<MistralProvider> logger)
        : base(logger)
    {
        _options = options.Value.Mistral ?? new MistralOptions();
    }

    public override void ConfigureKernel(IKernelBuilder builder)
    {
        if (string.IsNullOrEmpty(_options.ApiKey)) return;

        var modelId = _options.DefaultModel ?? "mistral-large-latest";
        builder.AddMistralChatCompletion(modelId, _options.ApiKey);
    }

    public override Task<IReadOnlyList<AIModel>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AIModel>>(GetStaticModels());
    }

    private static IReadOnlyList<AIModel> GetStaticModels() =>
    [
        new AIModel { Id = "mistral-large-latest", Name = "Mistral Large", Provider = AIProvider.Mistral,
            Capabilities = AICapability.Chat | AICapability.Streaming | AICapability.ToolCalling },
        new AIModel { Id = "mistral-small-latest", Name = "Mistral Small", Provider = AIProvider.Mistral,
            Capabilities = AICapability.Chat | AICapability.Streaming | AICapability.ToolCalling },
        new AIModel { Id = "mistral-embed", Name = "Mistral Embed", Provider = AIProvider.Mistral,
            Capabilities = AICapability.Embeddings },
    ];

    public override object? GetNativeClient() => null;
}
