using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Interfaces;

public interface IAuthService
{
    Task<Result<User>> LoginAsync(string username, string password);
}
