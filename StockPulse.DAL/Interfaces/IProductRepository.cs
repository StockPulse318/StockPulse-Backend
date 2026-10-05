using Microsoft.Data.Sqlite;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int productId);
    Task<Product?> GetByNameAsync(string productName);
    Task<IEnumerable<Product>> GetAllAsync();
    Task<IEnumerable<Product>> SearchByNameAsync(string partialName);
    Task<IEnumerable<Product>> GetLowStockAsync();
    Task<int> AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int productId);
    Task<IEnumerable<string>> GetBranchesAsync();
    Task<IEnumerable<string>> GetCategoriesAsync();

    // Accepts a caller-managed connection and transaction so the quantity update
    // and the log insert share one atomic unit in TransactionService.
    Task AdjustQuantityAsync(int productId, int delta, SqliteConnection connection, SqliteTransaction transaction);
}
