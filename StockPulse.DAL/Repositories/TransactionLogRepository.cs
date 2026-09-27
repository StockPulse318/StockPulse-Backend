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

    public async Task AddAsync(
        InventoryTransactionLog log,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        // This overload accepts the external connection and transaction deliberately —
        // the log insert must be part of the same atomic unit as the quantity update.
        // If this insert fails, the transaction is rolled back by the service layer,
        // preventing a stock change from occurring without a corresponding audit record.
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
