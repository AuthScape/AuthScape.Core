using System.Reflection;
using System.Text.Json;

namespace AuthScape.AI.Tools;

/// <summary>
/// Wraps a delegate/lambda as an IAITool instance for inline tool registration.
/// </summary>
internal class DelegateTool : IAITool
{
    private readonly Delegate _delegate;
    private readonly JsonElement _parametersSchema;

    public string Name { get; }
    public string Description { get; }
    public JsonElement ParametersSchema => _parametersSchema;

    public DelegateTool(string name, string description, Delegate handler)
    {
        Name = name;
        Description = description;
        _delegate = handler;
        _parametersSchema = BuildSchemaFromDelegate(handler);
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var method = _delegate.Method;
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            if (param.ParameterType == typeof(CancellationToken))
            {
                args[i] = cancellationToken;
                continue;
            }

            if (context.Arguments.ValueKind != JsonValueKind.Undefined &&
                context.Arguments.TryGetProperty(param.Name!, out var value))
            {
                args[i] = JsonSerializer.Deserialize(value.GetRawText(), param.ParameterType);
            }
            else if (param.HasDefaultValue)
            {
                args[i] = param.DefaultValue;
            }
            else
            {
                args[i] = param.ParameterType.IsValueType
                    ? Activator.CreateInstance(param.ParameterType)
                    : null;
            }
        }

        try
        {
            var result = _delegate.DynamicInvoke(args);

            if (result is Task<string> taskString)
                return ToolExecutionResult.Success(await taskString);
            if (result is Task<ToolExecutionResult> taskResult)
                return await taskResult;
            if (result is Task task)
            {
                await task;
                return ToolExecutionResult.Success("Completed");
            }

            return ToolExecutionResult.Success(result?.ToString() ?? "");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return ToolExecutionResult.Failure(ex.InnerException.Message);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message);
        }
    }

    private static JsonElement BuildSchemaFromDelegate(Delegate handler)
    {
        var parameters = handler.Method.GetParameters();
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            if (param.ParameterType == typeof(CancellationToken))
                continue;

            properties[param.Name!] = new
            {
                type = GetJsonType(param.ParameterType),
                description = param.Name
            };

            if (!param.HasDefaultValue)
                required.Add(param.Name!);
        }

        return JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties,
            required
        });
    }

    private static string GetJsonType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return "integer";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type.IsArray || type.IsAssignableTo(typeof(System.Collections.IEnumerable)) && type != typeof(string))
            return "array";
        return "object";
    }
}
