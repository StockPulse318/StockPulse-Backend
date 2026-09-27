using Microsoft.Data.Sqlite;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Interfaces;

public interface ITransactionLogRepository
{
    Task<IEnumerable<InventoryTransactionLog>> GetAllAsync();
    Task<IEnumerable<InventoryTransactionLog>> GetByProductIdAsync(int productId);
    Task<IEnumerable<InventoryTransactionLog>> GetByUserAsync(string username);

    /// <summary>
    /// Inserts a log record within a caller-managed transaction.
    /// Must always be paired with a corresponding AdjustQuantityAsync call
    /// inside the same transaction block to guarantee ledger consistency.
    /// </summary>
    Task AddAsync(InventoryTransactionLog log, SqliteConnection connection, SqliteTransaction transaction);
}
