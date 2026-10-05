using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<Product?> GetByCodeAsync(string productCode);
    Task<Product?> GetByNameAsync(string name);
    Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int limit,
        string? sortBy = null,
        string? sortOrder = null,
        int? categoryId = null,
        string? q = null);
    Task<IEnumerable<Product>> GetLowStockAsync();
    Task<int> AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);

    Task<(bool Success, string? ErrorCode, string? ErrorMessage)> ExecuteStockInAsync(int productId, int amount, int performedByUserId);
    Task<(bool Success, string? ErrorCode, string? ErrorMessage)> ExecuteStockOutAsync(int productId, int amount, int performedByUserId);
}
