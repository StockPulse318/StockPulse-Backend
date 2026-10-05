namespace StockPulse.Domain.Entities;

public sealed class StockMovement
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string Type { get; init; } = string.Empty;
    public int Amount { get; init; }
    public int PerformedBy { get; init; }
    public string CreatedAt { get; init; } = DateTime.UtcNow.ToString("o");
}

public static class StockMovementTypes
{
    public const string StockIn  = "STOCK_IN";
    public const string StockOut = "STOCK_OUT";

    public static readonly IReadOnlyList<string> All = [StockIn, StockOut];

    public static bool IsValid(string type) => All.Contains(type);
}
