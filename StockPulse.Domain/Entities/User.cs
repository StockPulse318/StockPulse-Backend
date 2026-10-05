namespace StockPulse.Domain.Entities;

public sealed class User
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Role { get; init; } = UserRoles.Clerk;
    public bool IsActive { get; init; } = true;
    public string CreatedAt { get; init; } = DateTime.UtcNow.ToString("o");

    public bool IsWarehouseManager => Role == UserRoles.WarehouseManager;
    public bool IsClerk => Role == UserRoles.Clerk;
}

// Single source of truth for role strings — referenced by BLL validation and DB CHECK constraints.
public static class UserRoles
{
    public const string WarehouseManager = "WAREHOUSE_MANAGER";
    public const string Clerk = "CLERK";

    public static readonly IReadOnlyList<string> All = [WarehouseManager, Clerk];

    public static bool IsValid(string role) => All.Contains(role);
}
