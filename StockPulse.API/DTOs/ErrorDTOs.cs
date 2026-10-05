using System.Text.Json.Serialization;

namespace StockPulse.API.DTOs;

public sealed record ErrorDetail(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

public sealed record ErrorResponse(
    [property: JsonPropertyName("error")] ErrorDetail Error);
