using AuthScape.AI.Enums;

namespace AuthScape.AI.Configuration;

public class AIServiceOptions
{
    public const string SectionName = "AuthScapeAI";

    public AIProvider DefaultProvider { get; set; } = AIProvider.Ollama;
    public OllamaOptions? Ollama { get; set; }
    public OpenAIOptions? OpenAI { get; set; }
    public AnthropicOptions? Anthropic { get; set; }
    public GoogleOptions? Google { get; set; }
    public MistralOptions? Mistral { get; set; }
    public OpenAICompatibleOptions? Groq { get; set; }
    public Dictionary<string, OpenAICompatibleOptions>? CustomProviders { get; set; }
}
