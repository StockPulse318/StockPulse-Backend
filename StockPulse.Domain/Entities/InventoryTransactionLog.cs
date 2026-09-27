namespace StockPulse.Domain.Entities;

/// <summary>
/// Immutable ledger record written once per stock movement.
/// Never updated or deleted directly — cascade delete from Products handles
/// orphan cleanup if a product is hard-deleted by a Warehouse Manager.
/// </summary>
public sealed class InventoryTransactionLog
{
    public int TransactionID { get; init; }
    public int ProductID { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public int QuantityChanged { get; init; }
    public string HandledBy { get; init; } = string.Empty;
    public string Timestamp { get; init; } = string.Empty;
}

/// <summary>
/// Canonical transaction type constants — mirrors the DB CHECK constraint
/// so violations are caught in the BLL before they ever hit SQLite.
/// </summary>
public static class TransactionTypes
{
    public const string StockIn = "Stock-In";
    public const string StockOut = "Stock-Out";

    public static readonly IReadOnlyList<string> All = [StockIn, StockOut];

    public static bool IsValid(string type) => All.Contains(type);
}
