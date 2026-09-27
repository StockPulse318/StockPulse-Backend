using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Interfaces;

public interface IProductService
{
    Task<Result<Product>> GetByIdAsync(int productId);
    Task<Result<IEnumerable<Product>>> GetAllAsync();
    Task<Result<IEnumerable<Product>>> SearchByNameAsync(string partialName);
    Task<Result<IEnumerable<Product>>> GetLowStockAsync();

    /// <summary>
    /// Creates a new product. Restricted to Warehouse Manager.
    /// </summary>
    Task<Result<int>> AddProductAsync(string actorUsername, Product product);

    /// <summary>
    /// Updates product metadata (name, category, price, reorder level).
    /// Quantity is never touched here — stock adjustments go through TransactionService.
    /// Restricted to Warehouse Manager.
    /// </summary>
    Task<Result> UpdateProductAsync(string actorUsername, Product product);

    /// <summary>
    /// Hard-deletes a product and cascades to its transaction logs.
    /// Restricted to Warehouse Manager.
    /// </summary>
    Task<Result> DeleteProductAsync(string actorUsername, int productId);
}
