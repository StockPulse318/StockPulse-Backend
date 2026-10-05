using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(int id);
    Task<Category?> GetByNameAsync(string name);
    Task<IEnumerable<Category>> GetAllAsync();
    Task<int> AddAsync(Category category);
}
