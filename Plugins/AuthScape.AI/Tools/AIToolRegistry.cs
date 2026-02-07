using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace AuthScape.AI.Tools;

/// <summary>
/// Default implementation of IAIToolRegistry. Supports attribute-based discovery,
/// interface-based registration, and inline lambda registration.
/// </summary>
public class AIToolRegistry : IAIToolRegistry
{
    private readonly ConcurrentDictionary<string, IAITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IAITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public void RegisterFromType(Type type, object? instance = null)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Static | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var attr = method.GetCustomAttribute<AIToolAttribute>();
            if (attr is null) continue;

            var target = method.IsStatic ? null : (instance ?? Activator.CreateInstance(type));
            var tool = new AttributeBasedTool(method, attr, target);
            _tools[tool.Name] = tool;
        }
    }

    public void RegisterFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var hasMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Static | BindingFlags.Instance)
                .Any(m => m.GetCustomAttribute<AIToolAttribute>() is not null);

            if (hasMethods)
            {
                RegisterFromType(type);
            }
        }
    }

    public void RegisterFunction(string name, string description, Delegate function)
    {
        var tool = new DelegateTool(name, description, function);
        _tools[tool.Name] = tool;
    }

    public IAITool? GetTool(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public IReadOnlyList<IAITool> GetAllTools()
    {
        return _tools.Values.ToList().AsReadOnly();
    }

    public bool Unregister(string name)
    {
        return _tools.TryRemove(name, out _);
    }

    public IReadOnlyList<AITool> ToAITools()
    {
        return _tools.Values.Select(CreateAITool).ToList().AsReadOnly();
    }

    public IReadOnlyList<AITool> ToAITools(IEnumerable<string> toolNames)
    {
        var names = new HashSet<string>(toolNames, StringComparer.OrdinalIgnoreCase);
        return _tools.Values
            .Where(t => names.Contains(t.Name))
            .Select(CreateAITool)
            .ToList()
            .AsReadOnly();
    }

    public KernelPlugin ToKernelPlugin(string pluginName = "AuthScapeTools")
    {
        var functions = new List<KernelFunction>();

        foreach (var tool in _tools.Values)
        {
            functions.Add(CreateKernelFunction(tool));
        }

        return KernelPluginFactory.CreateFromFunctions(pluginName, functions);
    }

    private static KernelFunction CreateKernelFunction(IAITool tool)
    {
        // Parse parameter metadata from the tool's JSON schema
        var parameters = new List<KernelParameterMetadata>();

        if (tool.ParametersSchema.ValueKind == JsonValueKind.Object &&
            tool.ParametersSchema.TryGetProperty("properties", out var properties))
        {
            var requiredSet = new HashSet<string>();
            if (tool.ParametersSchema.TryGetProperty("required", out var required))
            {
                foreach (var r in required.EnumerateArray())
                    requiredSet.Add(r.GetString()!);
            }

            foreach (var prop in properties.EnumerateObject())
            {
                var desc = prop.Value.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                var jsonType = prop.Value.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string";
                parameters.Add(new KernelParameterMetadata(prop.Name)
                {
                    Description = desc,
                    IsRequired = requiredSet.Contains(prop.Name),
                    ParameterType = MapJsonTypeToClrType(jsonType)
                });
            }
        }

        var capturedTool = tool;

        return KernelFunctionFactory.CreateFromMethod(
            async (KernelArguments arguments, CancellationToken cancellationToken) =>
            {
                var argsDict = new Dictionary<string, object?>();
                foreach (var kvp in arguments)
                    argsDict[kvp.Key] = kvp.Value;

                var argsElement = argsDict.Count > 0
                    ? JsonSerializer.SerializeToElement(argsDict)
                    : default;

                var context = new ToolExecutionContext
                {
                    ToolName = capturedTool.Name,
                    Arguments = argsElement
                };

                var result = await capturedTool.ExecuteAsync(context, cancellationToken);
                return result.Content;
            },
            functionName: capturedTool.Name,
            description: capturedTool.Description,
            parameters: parameters,
            returnParameter: new KernelReturnParameterMetadata { Description = "Tool result" });
    }

    private static Type MapJsonTypeToClrType(string jsonType) => jsonType switch
    {
        "string" => typeof(string),
        "integer" => typeof(int),
        "number" => typeof(double),
        "boolean" => typeof(bool),
        "array" => typeof(object[]),
        _ => typeof(object)
    };

    private static AITool CreateAITool(IAITool tool)
    {
        return AIFunctionFactory.Create(
            async (string argumentsJson) =>
            {
                var args = string.IsNullOrWhiteSpace(argumentsJson)
                    ? default
                    : JsonSerializer.Deserialize<JsonElement>(argumentsJson);

                var context = new ToolExecutionContext
                {
                    ToolName = tool.Name,
                    Arguments = args
                };

                var result = await tool.ExecuteAsync(context);
                return result.Content;
            },
            tool.Name,
            tool.Description);
    }
}
