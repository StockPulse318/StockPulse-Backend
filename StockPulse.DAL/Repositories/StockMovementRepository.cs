using Dapper;
using Microsoft.Data.Sqlite;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Repositories;

public sealed class StockMovementRepository : IStockMovementRepository
{
    private readonly DatabaseInitializer _db;

    private const string SelectColumns =
        """
        SELECT id AS Id, product_id AS ProductId, type AS Type,
               amount AS Amount, performed_by AS PerformedBy, created_at AS CreatedAt
        FROM stock_movements
        """;

    public StockMovementRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task<int> AddAsync(StockMovement movement, SqliteConnection connection, SqliteTransaction transaction)
    {
        return await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO stock_movements (product_id, type, amount, performed_by, created_at)
            VALUES (@ProductId, @Type, @Amount, @PerformedBy, @CreatedAt);
            SELECT last_insert_rowid();
            """,
            new
            {
                movement.ProductId,
                movement.Type,
                movement.Amount,
                movement.PerformedBy,
                movement.CreatedAt
            },
            transaction);
    }

    public async Task<IEnumerable<StockMovement>> GetByProductIdAsync(int productId)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<StockMovement>(
            $"{SelectColumns} WHERE product_id = @ProductId ORDER BY id DESC;",
            new { ProductId = productId });
    }

    public async Task<IEnumerable<StockMovement>> GetAllAsync()
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<StockMovement>(
            $"{SelectColumns} ORDER BY id DESC;");
    }
}
