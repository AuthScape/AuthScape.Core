namespace AuthScape.AI.Exceptions;

public class ToolExecutionException : AIServiceException
{
    public string ToolName { get; }

    public ToolExecutionException(string toolName, string message)
        : base($"Tool '{toolName}' execution failed: {message}")
    {
        ToolName = toolName;
    }

    public ToolExecutionException(string toolName, string message, Exception innerException)
        : base($"Tool '{toolName}' execution failed: {message}", innerException)
    {
        ToolName = toolName;
    }
}
