using Microsoft.Data.Sqlite;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Interfaces;

public interface ITransactionLogRepository
{
    Task<IEnumerable<InventoryTransactionLog>> GetAllAsync();
    Task<IEnumerable<InventoryTransactionLog>> GetByProductIdAsync(int productId);
    Task<IEnumerable<InventoryTransactionLog>> GetByUserAsync(string username);

    // Must be called within the same transaction as AdjustQuantityAsync.
    // A stock level change without a log entry — or vice versa — is a data integrity failure.
    Task AddAsync(InventoryTransactionLog log, SqliteConnection connection, SqliteTransaction transaction);
}
