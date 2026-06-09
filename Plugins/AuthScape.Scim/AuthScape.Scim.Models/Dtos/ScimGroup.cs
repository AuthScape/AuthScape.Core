using System.Text.Json.Serialization;

namespace AuthScape.Scim.Models.Dtos;

public class ScimGroup
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:schemas:core:2.0:Group" };

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("members")]
    public List<ScimGroupMember>? Members { get; set; }

    [JsonPropertyName("meta")]
    public ScimMeta? Meta { get; set; }
}

public class ScimGroupMember
{
    [JsonPropertyName("value")] public string Value { get; set; } = "";
    [JsonPropertyName("display")] public string? Display { get; set; }
    [JsonPropertyName("$ref")] public string? Ref { get; set; }
    /// <summary>"User" or "Group"</summary>
    [JsonPropertyName("type")] public string? Type { get; set; }
}
