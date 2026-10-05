using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Interfaces;

public interface ICategoryService
{
    Task<Result<IEnumerable<Category>>> GetAllAsync();
    Task<Result<Category>> GetByIdAsync(int id);
    Task<Result<Category>> AddAsync(string actorRole, string name);
}
