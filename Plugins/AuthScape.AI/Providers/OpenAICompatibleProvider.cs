using System.ClientModel;
using AuthScape.AI.Configuration;
using AuthScape.AI.Enums;
using AuthScape.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OpenAI;

namespace AuthScape.AI.Providers;

/// <summary>
/// Provider for any OpenAI-compatible API (Groq, Together, etc.).
/// Uses the official OpenAI SDK with a custom endpoint, registered via SK's OpenAI connector.
/// </summary>
public class OpenAICompatibleProvider : AIProviderBase
{
    private readonly OpenAICompatibleOptions _options;
    private readonly OpenAIClient? _client;
    private readonly AIProvider _providerType;

    public override AIProvider ProviderType => _providerType;

    public override ProviderCapabilities Capabilities => new()
    {
        SupportedCapabilities = AICapability.Chat | AICapability.Streaming
            | AICapability.ToolCalling | AICapability.Vision
    };

    public override bool IsConfigured =>
        !string.IsNullOrEmpty(_options.ApiKey) && !string.IsNullOrEmpty(_options.BaseUrl);

    public OpenAICompatibleProvider(
        IOptions<AIServiceOptions> options,
        ILogger<OpenAICompatibleProvider> logger)
        : base(logger)
    {
        _options = options.Value.Groq ?? new OpenAICompatibleOptions();
        _providerType = AIProvider.Groq;

        if (IsConfigured)
        {
            _client = new OpenAIClient(
                new ApiKeyCredential(_options.ApiKey!),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(_options.BaseUrl!)
                });
        }
    }

    public OpenAICompatibleProvider(
        OpenAICompatibleOptions providerOptions,
        AIProvider providerType,
        ILogger<OpenAICompatibleProvider> logger)
        : base(logger)
    {
        _options = providerOptions;
        _providerType = providerType;

        if (IsConfigured)
        {
            _client = new OpenAIClient(
                new ApiKeyCredential(_options.ApiKey!),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(_options.BaseUrl!)
                });
        }
    }

    public override void ConfigureKernel(IKernelBuilder builder)
    {
        if (_client is null) return;

        var modelId = _options.DefaultModel ?? "llama-3.3-70b-versatile";
        builder.AddOpenAIChatCompletion(modelId, _client);
    }

    public override Task<IReadOnlyList<AIModel>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var models = new List<AIModel>
        {
            new()
            {
                Id = _options.DefaultModel ?? "llama-3.3-70b-versatile",
                Name = _options.DefaultModel ?? "llama-3.3-70b-versatile",
                Provider = _providerType,
                Capabilities = AICapability.Chat | AICapability.Streaming | AICapability.ToolCalling
            }
        };

        return Task.FromResult<IReadOnlyList<AIModel>>(models.AsReadOnly());
    }

    public override object? GetNativeClient() => _client;
}
