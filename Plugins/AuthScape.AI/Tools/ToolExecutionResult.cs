namespace AuthScape.AI.Tools;

/// <summary>
/// Result from executing an AI tool.
/// </summary>
public class ToolExecutionResult
{
    public bool IsSuccess { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }

    public static ToolExecutionResult Success(string content) =>
        new() { IsSuccess = true, Content = content };

    public static ToolExecutionResult Failure(string error) =>
        new() { IsSuccess = false, ErrorMessage = error, Content = $"Error: {error}" };
}
