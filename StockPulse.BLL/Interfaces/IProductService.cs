using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Interfaces;

public interface IProductService
{
    Task<Result<Product>> GetByIdAsync(int productId);
    Task<Result<(IEnumerable<Product> Items, int TotalCount)>> GetPagedAsync(
        int page,
        int limit,
        string? sortBy = null,
        string? sortOrder = null,
        int? categoryId = null,
        string? q = null);
    Task<Result<IEnumerable<Product>>> GetLowStockAsync();

    Task<Result<Product>> AddProductAsync(string actorRole, Product product);
    Task<Result<Product>> UpdateProductAsync(string actorRole, Product product);
    Task<Result> DeleteProductAsync(string actorRole, int productId);

    Task<Result> StockInAsync(int productId, int amount, int performedByUserId);
    Task<Result> StockOutAsync(int productId, int amount, int performedByUserId);
}
