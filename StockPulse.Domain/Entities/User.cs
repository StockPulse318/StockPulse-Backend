namespace StockPulse.Domain.Entities;

/// <summary>
/// Represents an authenticated system user.
/// Role is constrained at the DB level but also validated in the BLL before any mutation.
/// </summary>
public sealed class User
{
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;

    public bool IsWarehouseManager => Role == UserRoles.WarehouseManager;
    public bool IsStockClerk => Role == UserRoles.StockClerk;
}

/// <summary>
/// Centralised role constants — kept here so BLL, DAL, and future UI layers
/// all reference one canonical source instead of scattered string literals.
/// </summary>
public static class UserRoles
{
    public const string WarehouseManager = "Warehouse Manager";
    public const string StockClerk = "Stock Clerk";

    public static readonly IReadOnlyList<string> All = [WarehouseManager, StockClerk];

    public static bool IsValid(string role) => All.Contains(role);
}
