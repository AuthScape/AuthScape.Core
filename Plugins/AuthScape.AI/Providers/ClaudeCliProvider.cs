using AuthScape.AI.Configuration;
using AuthScape.AI.Enums;
using AuthScape.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AuthScape.AI.Providers;

/// <summary>
/// AI provider that delegates to the local Claude CLI (claude command).
/// Requires the "claude" CLI tool to be installed and available on PATH.
/// </summary>
public class ClaudeCliProvider : AIProviderBase
{
    private readonly ClaudeCliOptions _options;

    public override AIProvider ProviderType => AIProvider.ClaudeCli;

    public override ProviderCapabilities Capabilities => new()
    {
        SupportedCapabilities = AICapability.Chat | AICapability.Streaming
    };

    // Claude CLI is considered configured if it's enabled (always true since
    // it uses the locally installed CLI — no API key required).
    // Set an ApiKey value in config to explicitly enable it.
    public override bool IsConfigured => true;

    public ClaudeCliProvider(IOptions<AIServiceOptions> options, ILogger<ClaudeCliProvider> logger)
        : base(logger)
    {
        _options = options.Value.ClaudeCli ?? new ClaudeCliOptions();
    }

    public override void ConfigureKernel(IKernelBuilder builder)
    {
        var service = new ClaudeCliChatCompletionService(_options);
        builder.Services.AddSingleton<IChatCompletionService>(service);
    }

    public override Task<IReadOnlyList<AIModel>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        // Claude CLI uses whatever model is configured in the user's Claude Code settings
        var models = new List<AIModel>
        {
            CreateModel("claude-cli-default", "Claude CLI (Default)")
        };

        return Task.FromResult<IReadOnlyList<AIModel>>(models.AsReadOnly());
    }

    private static AIModel CreateModel(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Provider = AIProvider.ClaudeCli,
        Capabilities = AICapability.Chat | AICapability.Streaming
    };
}
