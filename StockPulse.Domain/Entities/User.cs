namespace StockPulse.Domain.Entities;

public sealed class User
{
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;

    public bool IsWarehouseManager => Role == UserRoles.WarehouseManager;
    public bool IsStockClerk => Role == UserRoles.StockClerk;
}

// Single source of truth for role strings — referenced by BLL validation and DB CHECK constraints.
public static class UserRoles
{
    public const string WarehouseManager = "Warehouse Manager";
    public const string StockClerk = "Stock Clerk";

    public static readonly IReadOnlyList<string> All = [WarehouseManager, StockClerk];

    public static bool IsValid(string role) => All.Contains(role);
}
