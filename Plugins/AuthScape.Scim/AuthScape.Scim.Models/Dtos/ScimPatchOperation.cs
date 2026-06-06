using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthScape.Scim.Models.Dtos;

public class ScimPatchRequest
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:api:messages:2.0:PatchOp" };

    [JsonPropertyName("Operations")]
    public List<ScimPatchOperation> Operations { get; set; } = new();
}

public class ScimPatchOperation
{
    /// <summary>"add", "remove", or "replace" (case-insensitive per RFC).</summary>
    [JsonPropertyName("op")]
    public string Op { get; set; } = "";

    /// <summary>
    /// Optional path expression. Examples:
    ///   "userName"
    ///   "name.givenName"
    ///   "emails[type eq \"work\"].value"
    ///   "members"
    /// When omitted, op applies to the whole resource (Value should be a partial resource representation).
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>JSON value — a string, object, array, or null. Stored as raw element; parsed by the processor.</summary>
    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }
}
