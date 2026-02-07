namespace AuthScape.AI.Configuration;

public class AnthropicOptions : ProviderOptions
{
    public string? ApiVersion { get; set; }
    public int MaxTokensDefault { get; set; } = 4096;
}
