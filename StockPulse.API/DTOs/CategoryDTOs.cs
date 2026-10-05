using System.Text.Json.Serialization;

namespace StockPulse.API.DTOs;

public sealed record CategoryResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name);

public sealed record CreateCategoryRequest(
    [property: JsonPropertyName("name")] string Name);
