namespace StockPulse.Domain.Entities;

// Written once per stock movement, never updated directly.
// Cascade delete from Products handles cleanup when a product is removed.
public sealed class InventoryTransactionLog
{
    public int TransactionID { get; init; }
    public int ProductID { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public int QuantityChanged { get; init; }
    public string HandledBy { get; init; } = string.Empty;
    public string Timestamp { get; init; } = string.Empty;
}

// Mirrors the DB CHECK constraint so the BLL catches invalid values before they hit SQLite.
public static class TransactionTypes
{
    public const string StockIn  = "Stock-In";
    public const string StockOut = "Stock-Out";

    public static readonly IReadOnlyList<string> All = [StockIn, StockOut];

    public static bool IsValid(string type) => All.Contains(type);
}
