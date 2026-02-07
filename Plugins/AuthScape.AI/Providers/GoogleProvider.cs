using AuthScape.AI.Configuration;
using AuthScape.AI.Enums;
using AuthScape.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;

namespace AuthScape.AI.Providers;

public class GoogleProvider : AIProviderBase
{
    private readonly GoogleOptions _options;

    public override AIProvider ProviderType => AIProvider.Google;

    public override ProviderCapabilities Capabilities => new()
    {
        SupportedCapabilities = AICapability.Chat | AICapability.Streaming
            | AICapability.Vision | AICapability.ToolCalling
    };

    public override bool IsConfigured => !string.IsNullOrEmpty(_options.ApiKey);

    public GoogleProvider(IOptions<AIServiceOptions> options, ILogger<GoogleProvider> logger)
        : base(logger)
    {
        _options = options.Value.Google ?? new GoogleOptions();
    }

    public override void ConfigureKernel(IKernelBuilder builder)
    {
        if (string.IsNullOrEmpty(_options.ApiKey)) return;

        var modelId = _options.DefaultModel ?? "gemini-2.0-flash";
        builder.AddGoogleAIGeminiChatCompletion(modelId, _options.ApiKey);
    }

    public override Task<IReadOnlyList<AIModel>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var models = new List<AIModel>
        {
            CreateModel("gemini-2.0-flash", "Gemini 2.0 Flash"),
            CreateModel("gemini-2.0-flash-lite", "Gemini 2.0 Flash Lite"),
            CreateModel("gemini-1.5-pro", "Gemini 1.5 Pro"),
            CreateModel("gemini-1.5-flash", "Gemini 1.5 Flash"),
        };

        return Task.FromResult<IReadOnlyList<AIModel>>(models.AsReadOnly());
    }

    private static AIModel CreateModel(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Provider = AIProvider.Google,
        Capabilities = AICapability.Chat | AICapability.Streaming | AICapability.Vision | AICapability.ToolCalling
    };

    public override object? GetNativeClient() => null;
}
