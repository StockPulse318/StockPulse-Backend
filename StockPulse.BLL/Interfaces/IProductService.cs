using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Interfaces;

public interface IProductService
{
    Task<Result<Product>> GetByIdAsync(int productId);
    Task<Result<IEnumerable<Product>>> GetAllAsync();
    Task<Result<IEnumerable<Product>>> SearchByNameAsync(string partialName);
    Task<Result<IEnumerable<Product>>> GetLowStockAsync();

    Task<Result<int>> AddProductAsync(string actorUsername, Product product);
    Task<Result> UpdateProductAsync(string actorUsername, Product product);
    Task<Result> DeleteProductAsync(string actorUsername, int productId);
}
