using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<IEnumerable<User>> GetAllAsync();
    Task AddAsync(User user);
    Task DeleteAsync(string username);
}
