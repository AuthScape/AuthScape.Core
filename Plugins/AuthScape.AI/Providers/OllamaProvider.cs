using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AuthScape.AI.Configuration;
using AuthScape.AI.Enums;
using AuthScape.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;

namespace AuthScape.AI.Providers;

public class OllamaProvider : AIProviderBase, IModelManager
{
    private readonly OllamaOptions _options;
    private readonly Uri _endpoint;
    private readonly HttpClient _httpClient;

    public override AIProvider ProviderType => AIProvider.Ollama;

    public override ProviderCapabilities Capabilities => new()
    {
        SupportedCapabilities = AICapability.Chat | AICapability.Streaming
            | AICapability.Embeddings | AICapability.Vision
            | AICapability.ToolCalling | AICapability.ModelManagement
    };

    public override bool IsConfigured => true;

    public OllamaProvider(IOptions<AIServiceOptions> options, ILogger<OllamaProvider> logger)
        : base(logger)
    {
        _options = options.Value.Ollama ?? new OllamaOptions();
        _endpoint = new Uri(_options.BaseUrl ?? "http://localhost:11434");
        _httpClient = new HttpClient { BaseAddress = _endpoint };
    }

    public override void ConfigureKernel(IKernelBuilder builder)
    {
        var modelId = _options.DefaultModel ?? "llama3.2";
        builder.AddOllamaChatCompletion(modelId, _endpoint);
        builder.AddOllamaEmbeddingGenerator(modelId, _endpoint);
    }

    public override async Task<IReadOnlyList<AIModel>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(
                "/api/tags", cancellationToken);

            if (response.TryGetProperty("models", out var modelsArray))
            {
                return modelsArray.EnumerateArray().Select(m => new AIModel
                {
                    Id = m.GetProperty("name").GetString() ?? "",
                    Name = m.GetProperty("name").GetString() ?? "",
                    Provider = AIProvider.Ollama,
                    Capabilities = AICapability.Chat | AICapability.Streaming | AICapability.Embeddings,
                    ModifiedAt = m.TryGetProperty("modified_at", out var mod) && mod.TryGetDateTime(out var dt)
                        ? dt : DateTime.UtcNow
                }).ToList().AsReadOnly();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to list Ollama models");
        }

        return [];
    }

    public override async Task<AIModelDetails?> GetModelDetailsAsync(
        string modelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { name = modelId };
            var response = await _httpClient.PostAsJsonAsync("/api/show", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            return new AIModelDetails
            {
                Id = modelId,
                Name = modelId,
                Provider = AIProvider.Ollama,
                Family = json.TryGetProperty("details", out var d) && d.TryGetProperty("family", out var f)
                    ? f.GetString() : null,
                QuantizationLevel = d.TryGetProperty("quantization_level", out var q)
                    ? q.GetString() : null,
                License = json.TryGetProperty("license", out var l) ? l.GetString() : null,
                SystemPrompt = json.TryGetProperty("system", out var s) ? s.GetString() : null,
                Template = json.TryGetProperty("template", out var t) ? t.GetString() : null
            };
        }
        catch
        {
            return null;
        }
    }

    public override object? GetNativeClient() => _httpClient;

    // ── IModelManager ────────────────────────────────────────────────

    public async IAsyncEnumerable<ModelPullProgress> PullModelAsync(
        string modelName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new { name = modelName, stream = true };
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/pull")
        {
            Content = JsonContent.Create(request)
        };

        var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (line.Length == 0) continue;

            var json = JsonSerializer.Deserialize<JsonElement>(line);

            yield return new ModelPullProgress
            {
                Status = json.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "",
                PercentComplete = json.TryGetProperty("completed", out var c) && json.TryGetProperty("total", out var tot)
                    && c.TryGetInt64(out var completed) && tot.TryGetInt64(out var total) && total > 0
                    ? (double)completed / total * 100 : null,
                BytesCompleted = json.TryGetProperty("completed", out var bc) && bc.TryGetInt64(out var bv) ? bv : null,
                BytesTotal = json.TryGetProperty("total", out var bt) && bt.TryGetInt64(out var tv) ? tv : null
            };
        }
    }

    public async Task<bool> DeleteModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/delete")
            {
                Content = JsonContent.Create(new { name = modelName })
            };
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CopyModelAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/copy",
                new { source, destination },
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    protected override void DisposeResources()
    {
        _httpClient.Dispose();
    }
}
