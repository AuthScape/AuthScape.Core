using System.Text.Json;

namespace AuthScape.AI.Tools;

/// <summary>
/// Represents a tool that can be invoked by an AI model during function calling.
/// </summary>
public interface IAITool
{
    /// <summary>Unique name of the tool (used by the model to reference it).</summary>
    string Name { get; }

    /// <summary>Description of what the tool does (sent to the model).</summary>
    string Description { get; }

    /// <summary>JSON Schema describing the tool's parameters.</summary>
    JsonElement ParametersSchema { get; }

    /// <summary>Executes the tool with the given arguments.</summary>
    Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}
