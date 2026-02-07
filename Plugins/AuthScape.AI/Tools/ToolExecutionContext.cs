using System.Text.Json;

namespace AuthScape.AI.Tools;

/// <summary>
/// Context passed to a tool during execution, containing the arguments and service provider.
/// </summary>
public class ToolExecutionContext
{
    public string ToolName { get; init; } = string.Empty;
    public JsonElement Arguments { get; init; }
    public IServiceProvider? ServiceProvider { get; init; }

    /// <summary>Gets a required argument by name.</summary>
    public T GetArgument<T>(string name)
    {
        if (Arguments.ValueKind != JsonValueKind.Undefined &&
            Arguments.TryGetProperty(name, out var prop))
        {
            return JsonSerializer.Deserialize<T>(prop.GetRawText())!;
        }

        throw new ArgumentException($"Required argument '{name}' was not provided.", name);
    }

    /// <summary>Gets an optional argument by name, returning a default if not present.</summary>
    public T? GetOptionalArgument<T>(string name, T? defaultValue = default)
    {
        if (Arguments.ValueKind != JsonValueKind.Undefined &&
            Arguments.TryGetProperty(name, out var prop))
        {
            return JsonSerializer.Deserialize<T>(prop.GetRawText());
        }

        return defaultValue;
    }
}
