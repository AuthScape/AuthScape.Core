using System.Text.Json.Serialization;

namespace AuthScape.Scim.Models.Dtos;

public class ScimListResponse<T>
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:api:messages:2.0:ListResponse" };

    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    [JsonPropertyName("startIndex")]
    public int StartIndex { get; set; } = 1;   // SCIM is 1-based

    [JsonPropertyName("itemsPerPage")]
    public int ItemsPerPage { get; set; }

    [JsonPropertyName("Resources")]
    public List<T> Resources { get; set; } = new();
}

public class ScimQuery
{
    public string? Filter { get; set; }
    public int StartIndex { get; set; } = 1;
    public int Count { get; set; } = 100;
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }   // "ascending" or "descending"
    public string? Attributes { get; set; }   // comma-separated; null = all
    public string? ExcludedAttributes { get; set; }
}

public class ScimError
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:api:messages:2.0:Error" };

    [JsonPropertyName("status")]
    public string Status { get; set; } = "400";

    [JsonPropertyName("scimType")]
    public string? ScimType { get; set; }

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = "";
}
