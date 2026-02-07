namespace AuthScape.AI.Tools;

/// <summary>
/// Marks a method as an AI tool that can be discovered and registered automatically.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AIToolAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }

    public AIToolAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
