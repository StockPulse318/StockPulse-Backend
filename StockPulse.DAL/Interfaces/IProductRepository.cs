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

    /// <summary>
    /// Adjusts stock quantity by a signed delta within a caller-managed transaction.
    /// Positive delta = Stock-In, negative delta = Stock-Out.
    /// The caller is responsible for opening and committing/rolling back the transaction —
    /// this method deliberately does not manage its own transaction so it can participate
    /// in a larger atomic unit alongside the log insert.
    /// </summary>
    Task AdjustQuantityAsync(int productId, int delta, Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction);
}
