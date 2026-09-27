using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Interfaces;

public interface IAuthService
{
    Task<Result<User>> LoginAsync(string username, string password);

    Task<Result> RegisterUserAsync(string actorUsername, string newUsername, string password, string role);
    Task<Result> DeleteUserAsync(string actorUsername, string targetUsername);
    Task<Result<IEnumerable<User>>> GetAllUsersAsync(string actorUsername);
}
