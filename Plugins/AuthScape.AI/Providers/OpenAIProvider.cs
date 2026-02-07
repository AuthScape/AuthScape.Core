using AuthScape.AI.Configuration;
using AuthScape.AI.Enums;
using AuthScape.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OpenAI;

namespace AuthScape.AI.Providers;

public class OpenAIProvider : AIProviderBase
{
    private readonly OpenAIOptions _options;
    private readonly OpenAIClient? _client;

    public override AIProvider ProviderType => AIProvider.OpenAI;

    public override ProviderCapabilities Capabilities => new()
    {
        SupportedCapabilities = AICapability.Chat | AICapability.Streaming
            | AICapability.Embeddings | AICapability.Vision
            | AICapability.ToolCalling | AICapability.Audio
            | AICapability.ImageGeneration
    };

    public override bool IsConfigured => !string.IsNullOrEmpty(_options.ApiKey);

    public OpenAIProvider(IOptions<AIServiceOptions> options, ILogger<OpenAIProvider> logger)
        : base(logger)
    {
        _options = options.Value.OpenAI ?? new OpenAIOptions();
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _client = new OpenAIClient(_options.ApiKey);
        }
    }

    public override void ConfigureKernel(IKernelBuilder builder)
    {
        if (_client is null) return;

        var modelId = _options.DefaultModel ?? "gpt-4o-mini";
        builder.AddOpenAIChatCompletion(modelId, _client);
        builder.AddOpenAIEmbeddingGenerator("text-embedding-3-small", _client);
    }

    public override async Task<IReadOnlyList<AIModel>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_client is null) return [];

        var result = new List<AIModel>();
        var modelsClient = _client.GetOpenAIModelClient();
        var response = await modelsClient.GetModelsAsync(cancellationToken);
        var models = response.Value;

        foreach (var model in models)
        {
            result.Add(new AIModel
            {
                Id = model.Id,
                Name = model.Id,
                Provider = AIProvider.OpenAI,
                Capabilities = AICapability.Chat | AICapability.Streaming | AICapability.ToolCalling,
                ModifiedAt = model.CreatedAt.UtcDateTime
            });
        }

        return result.AsReadOnly();
    }

    public override object? GetNativeClient() => _client;
}
