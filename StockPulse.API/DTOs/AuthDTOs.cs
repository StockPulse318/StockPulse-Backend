namespace StockPulse.API.DTOs;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string Token,
    string Username,
    string Role,
    string FullName = "",
    string AssignedBranch = "All Branches");

public sealed record RegisterUserRequest(
    string Username,
    string Password,
    string Role,
    string? FullName = null,
    string? AssignedBranch = null);

public sealed record UpdateUserRequest(
    string? FullName,
    string? Role,
    string? AssignedBranch,
    bool? IsActive);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record UserResponse(
    string Username,
    string Role,
    string FullName = "",
    string AssignedBranch = "All Branches",
    bool IsActive = true,
    string CreatedAt = "");
