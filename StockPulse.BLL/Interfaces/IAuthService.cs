using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Validates credentials and returns the authenticated User on success.
    /// Returns Failure (not an exception) for bad credentials so the UI can
    /// display a login error without try/catch at the presentation layer.
    /// </summary>
    Task<Result<User>> LoginAsync(string username, string password);

    Task<Result> RegisterUserAsync(string actorUsername, string newUsername, string password, string role);
    Task<Result> DeleteUserAsync(string actorUsername, string targetUsername);
    Task<Result<IEnumerable<User>>> GetAllUsersAsync(string actorUsername);
}
