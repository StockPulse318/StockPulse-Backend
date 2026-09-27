namespace StockPulse.Domain.Entities;

/// <summary>
/// Core inventory entity. IsLowStock is a derived, read-only flag computed
/// from live DB values — used by the UI alert engine without an extra query.
/// </summary>
public sealed class Product
{
    public int ProductID { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public int ReorderLevel { get; init; }

    /// <summary>
    /// Computed inline from the values already fetched — zero overhead,
    /// and always consistent with what was read from the DB in this snapshot.
    /// </summary>
    public bool IsLowStock => Quantity <= ReorderLevel;
}
