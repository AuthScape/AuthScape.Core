using System.Text.Json.Serialization;

namespace AuthScape.Scim.Models.Dtos;

/// <summary>
/// SCIM 2.0 User resource. Fields named to serialize directly to/from the wire format.
/// Subset of RFC 7643 §4.1 — covers the attributes most IdPs (Okta, Azure AD, OneLogin) actually send.
/// </summary>
public class ScimUser
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:schemas:core:2.0:User" };

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = "";

    [JsonPropertyName("name")]
    public ScimName? Name { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; } = true;

    [JsonPropertyName("emails")]
    public List<ScimEmail>? Emails { get; set; }

    [JsonPropertyName("phoneNumbers")]
    public List<ScimMultiValue>? PhoneNumbers { get; set; }

    [JsonPropertyName("groups")]
    public List<ScimGroupRef>? Groups { get; set; }

    [JsonPropertyName("meta")]
    public ScimMeta? Meta { get; set; }
}

public class ScimName
{
    [JsonPropertyName("givenName")] public string? GivenName { get; set; }
    [JsonPropertyName("familyName")] public string? FamilyName { get; set; }
    [JsonPropertyName("formatted")] public string? Formatted { get; set; }
}

public class ScimEmail
{
    [JsonPropertyName("value")] public string Value { get; set; } = "";
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("primary")] public bool Primary { get; set; }
}

public class ScimMultiValue
{
    [JsonPropertyName("value")] public string Value { get; set; } = "";
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("primary")] public bool Primary { get; set; }
}

public class ScimGroupRef
{
    [JsonPropertyName("value")] public string Value { get; set; } = "";
    [JsonPropertyName("display")] public string? Display { get; set; }
    [JsonPropertyName("$ref")] public string? Ref { get; set; }
}

public class ScimMeta
{
    [JsonPropertyName("resourceType")] public string ResourceType { get; set; } = "";
    [JsonPropertyName("created")] public DateTime Created { get; set; }
    [JsonPropertyName("lastModified")] public DateTime LastModified { get; set; }
    [JsonPropertyName("location")] public string? Location { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
}
