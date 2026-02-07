namespace AuthScape.AI.Configuration;

public class OpenAIOptions : ProviderOptions
{
    public string? Organization { get; set; }
    public string? ProjectId { get; set; }
}
