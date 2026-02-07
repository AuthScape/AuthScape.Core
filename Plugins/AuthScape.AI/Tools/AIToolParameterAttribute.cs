namespace AuthScape.AI.Tools;

/// <summary>
/// Describes a parameter of an AI tool method for schema generation.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public class AIToolParameterAttribute : Attribute
{
    public string Description { get; }
    public bool IsRequired { get; set; } = true;

    public AIToolParameterAttribute(string description)
    {
        Description = description;
    }
}
