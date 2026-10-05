using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<IEnumerable<User>> GetAllAsync();
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task UpdatePasswordAsync(string username, string passwordHash);
    Task DeleteAsync(string username);
}
