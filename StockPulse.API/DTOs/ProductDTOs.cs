namespace StockPulse.API.DTOs;

public sealed record ProductResponse(
    int ProductID,
    string ProductName,
    string Category,
    int Quantity,
    decimal UnitPrice,
    int ReorderLevel,
    bool IsLowStock);

public sealed record CreateProductRequest(
    string ProductName,
    string Category,
    int Quantity,
    decimal UnitPrice,
    int ReorderLevel);

public sealed record UpdateProductRequest(
    string ProductName,
    string Category,
    decimal UnitPrice,
    int ReorderLevel);
