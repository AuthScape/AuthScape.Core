using System.Text.Json;

namespace AuthScape.AI.Tools;

/// <summary>
/// Abstract base class for implementing AI tools with reduced boilerplate.
/// </summary>
public abstract class AIToolBase : IAITool
{
    public string Name { get; }
    public string Description { get; }

    private JsonElement? _parametersSchema;
    public JsonElement ParametersSchema => _parametersSchema ??= BuildParametersSchema();

    protected AIToolBase(string name, string description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>
    /// Override to define the JSON Schema for this tool's parameters.
    /// Default returns an empty object schema.
    /// </summary>
    protected virtual JsonElement BuildParametersSchema()
    {
        return JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { }
        });
    }

    public abstract Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}
