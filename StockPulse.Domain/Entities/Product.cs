namespace StockPulse.Domain.Entities;

public sealed class Product
{
    public int Id { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public int ReorderLevel { get; init; }
    public string CreatedAt { get; init; } = DateTime.UtcNow.ToString("o");
    public string UpdatedAt { get; init; } = DateTime.UtcNow.ToString("o");

    // Evaluated from the snapshot — quantity <= reorder_level
    public bool IsLowStock => Quantity <= ReorderLevel;
}
