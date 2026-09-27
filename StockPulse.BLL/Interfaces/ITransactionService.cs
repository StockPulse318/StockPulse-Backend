using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Interfaces;

public interface ITransactionService
{
    /// <summary>
    /// Atomically increments product stock and writes a Stock-In log record.
    /// Available to both Warehouse Manager and Stock Clerk.
    /// </summary>
    Task<Result> StockInAsync(string actorUsername, int productId, int quantity);

    /// <summary>
    /// Atomically decrements product stock and writes a Stock-Out log record.
    /// Aborts and returns a typed Failure (wrapping InsufficientStockException)
    /// if the requested quantity exceeds current stock.
    /// Available to both Warehouse Manager and Stock Clerk.
    /// </summary>
    Task<Result> StockOutAsync(string actorUsername, int productId, int quantity);

    Task<Result<IEnumerable<InventoryTransactionLog>>> GetAllLogsAsync(string actorUsername);
    Task<Result<IEnumerable<InventoryTransactionLog>>> GetLogsByProductAsync(string actorUsername, int productId);
    Task<Result<IEnumerable<InventoryTransactionLog>>> GetLogsByUserAsync(string actorUsername, string targetUsername);
}
