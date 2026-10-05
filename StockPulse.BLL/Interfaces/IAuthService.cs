using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Interfaces;

public interface IAuthService
{
    Task<Result<User>> LoginAsync(string username, string password);

    Task<Result> RegisterUserAsync(
        string actorUsername,
        string newUsername,
        string password,
        string role,
        string? fullName = null,
        string? assignedBranch = null);

    Task<Result> UpdateUserAsync(
        string actorUsername,
        string targetUsername,
        string? fullName,
        string? role,
        string? assignedBranch,
        bool? isActive);

    Task<Result> ResetPasswordAsync(
        string actorUsername,
        string targetUsername,
        string newPassword);

    Task<Result> DeleteUserAsync(string actorUsername, string targetUsername);
    Task<Result<IEnumerable<User>>> GetAllUsersAsync(string actorUsername);
}
