namespace StockPulse.API.DTOs;

public sealed record ProductResponse(
    int ProductID,
    string ProductName,
    string Category,
    int Quantity,
    decimal UnitPrice,
    int ReorderLevel,
    bool IsLowStock,
    string Branch = "Main Warehouse");

public sealed record CreateProductRequest(
    string ProductName,
    string Category,
    int Quantity,
    decimal UnitPrice,
    int ReorderLevel,
    string? Branch = null);

public sealed record UpdateProductRequest(
    string ProductName,
    string Category,
    decimal UnitPrice,
    int ReorderLevel,
    string? Branch = null);
