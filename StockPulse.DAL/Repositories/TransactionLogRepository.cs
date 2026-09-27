using Dapper;
using Microsoft.Data.Sqlite;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Repositories;

public sealed class TransactionLogRepository : ITransactionLogRepository
{
    private readonly DatabaseInitializer _db;

    private const string SelectAllColumns =
        """
        SELECT TransactionID, ProductID, TransactionType,
               QuantityChanged, HandledBy, Timestamp
        FROM InventoryTransactionLogs
        """;

    public TransactionLogRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task<IEnumerable<InventoryTransactionLog>> GetAllAsync()
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<InventoryTransactionLog>(
            $"{SelectAllColumns} ORDER BY Timestamp DESC;");
    }

    public async Task<IEnumerable<InventoryTransactionLog>> GetByProductIdAsync(int productId)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<InventoryTransactionLog>(
            $"{SelectAllColumns} WHERE ProductID = @ProductID ORDER BY Timestamp DESC;",
            new { ProductID = productId });
    }

    public async Task<IEnumerable<InventoryTransactionLog>> GetByUserAsync(string username)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<InventoryTransactionLog>(
            $"{SelectAllColumns} WHERE HandledBy = @HandledBy ORDER BY Timestamp DESC;",
            new { HandledBy = username });
    }

    // Accepts an external connection and transaction — this insert must be atomic
    // with the quantity update in ProductRepository. Both are committed or rolled back together.
    public async Task AddAsync(
        InventoryTransactionLog log,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO InventoryTransactionLogs
                (ProductID, TransactionType, QuantityChanged, HandledBy)
            VALUES
                (@ProductID, @TransactionType, @QuantityChanged, @HandledBy);
            """,
            new
            {
                log.ProductID,
                log.TransactionType,
                log.QuantityChanged,
                log.HandledBy
            },
            transaction);
    }
}
