namespace AuthScape.AI.Configuration;

public class ProviderOptions
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? DefaultModel { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxRetries { get; set; } = 2;
}
