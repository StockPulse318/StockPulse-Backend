using System.Text.Json.Serialization;

namespace StockPulse.API.DTOs;

public sealed record ProductResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("product_code")] string ProductCode,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category_id")] int CategoryId,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice,
    [property: JsonPropertyName("reorder_level")] int ReorderLevel,
    [property: JsonPropertyName("is_low_stock")] bool IsLowStock,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("updated_at")] string UpdatedAt);

public sealed record CreateProductRequest(
    [property: JsonPropertyName("product_code")] string ProductCode,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category_id")] int CategoryId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice,
    [property: JsonPropertyName("reorder_level")] int ReorderLevel);

public sealed record UpdateProductRequest(
    [property: JsonPropertyName("product_code")] string ProductCode,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category_id")] int CategoryId,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice,
    [property: JsonPropertyName("reorder_level")] int ReorderLevel);

public sealed record PagedProductsResponse(
    [property: JsonPropertyName("items")] IEnumerable<ProductResponse> Items,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("total_pages")] int TotalPages);
