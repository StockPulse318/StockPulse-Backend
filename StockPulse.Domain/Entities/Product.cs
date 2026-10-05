namespace StockPulse.Domain.Entities;

public sealed class Product
{
    public int ProductID { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Branch { get; init; } = "Main Warehouse";
    public string Category { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public int ReorderLevel { get; init; }

    // Evaluated from the already-fetched snapshot — no extra query needed.
    public bool IsLowStock => Quantity <= ReorderLevel;
}
