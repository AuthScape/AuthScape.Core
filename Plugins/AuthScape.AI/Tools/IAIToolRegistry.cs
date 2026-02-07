using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace AuthScape.AI.Tools;

/// <summary>
/// Registry for discovering, storing, and resolving AI tools.
/// </summary>
public interface IAIToolRegistry
{
    /// <summary>Registers a tool instance.</summary>
    void Register(IAITool tool);

    /// <summary>Registers all methods decorated with [AITool] in the given type.</summary>
    void RegisterFromType(Type type, object? instance = null);

    /// <summary>Registers all methods decorated with [AITool] found in the given assembly.</summary>
    void RegisterFromAssembly(Assembly assembly);

    /// <summary>Registers a delegate/lambda as a tool.</summary>
    void RegisterFunction(string name, string description, Delegate function);

    /// <summary>Gets a tool by name.</summary>
    IAITool? GetTool(string name);

    /// <summary>Gets all registered tools.</summary>
    IReadOnlyList<IAITool> GetAllTools();

    /// <summary>Removes a tool by name.</summary>
    bool Unregister(string name);

    /// <summary>Converts all registered tools to AITool instances for Microsoft.Extensions.AI.</summary>
    IReadOnlyList<AITool> ToAITools();

    /// <summary>Converts specified tools to AITool instances for Microsoft.Extensions.AI.</summary>
    IReadOnlyList<AITool> ToAITools(IEnumerable<string> toolNames);

    /// <summary>Converts all registered tools to a Semantic Kernel KernelPlugin.</summary>
    KernelPlugin ToKernelPlugin(string pluginName = "AuthScapeTools");
}
