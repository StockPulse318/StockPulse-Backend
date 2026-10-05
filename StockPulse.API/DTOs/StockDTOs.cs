using System.Text.Json.Serialization;

namespace StockPulse.API.DTOs;

public sealed record StockMovementRequest(
    [property: JsonPropertyName("amount")] int Amount);

public sealed record StockMovementResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("product_id")] int ProductId,
    [property: JsonPropertyName("amount")] int Amount);
