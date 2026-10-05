namespace StockPulse.Domain.Entities;

public sealed class User
{
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Role { get; init; } = UserRoles.StockClerk;
    public string FullName { get; init; } = string.Empty;
    public string AssignedBranch { get; init; } = "All Branches";
    public bool IsActive { get; init; } = true;
    public string CreatedAt { get; init; } = DateTime.UtcNow.ToString("o");

    public bool IsAdmin => Role == UserRoles.Administrator;
    public bool IsWarehouseManager => Role is UserRoles.WarehouseManager or UserRoles.Administrator;
    public bool IsStockClerk => Role == UserRoles.StockClerk;
}

// Single source of truth for role strings — referenced by BLL validation and DB CHECK constraints.
public static class UserRoles
{
    public const string Administrator = "Administrator";
    public const string WarehouseManager = "Warehouse Manager";
    public const string StockClerk = "Stock Clerk";

    public static readonly IReadOnlyList<string> All = [Administrator, WarehouseManager, StockClerk];

    public static bool IsValid(string role) => All.Contains(role);
}
