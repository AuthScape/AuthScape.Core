namespace AuthScape.AI.Configuration;

public class ClaudeCliOptions : ProviderOptions
{
    /// <summary>
    /// Path to the claude CLI executable. Defaults to "claude" (expects it on PATH).
    /// </summary>
    public string CliPath { get; set; } = "claude";
}
