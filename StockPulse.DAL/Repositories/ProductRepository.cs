using Dapper;
using Microsoft.Data.Sqlite;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly DatabaseInitializer _db;

    private const string SelectAllColumns =
        "SELECT ProductID, ProductName, Category, Quantity, UnitPrice, ReorderLevel FROM Products";

    public ProductRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task<Product?> GetByIdAsync(int productId)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Product>(
            $"{SelectAllColumns} WHERE ProductID = @ProductID;",
            new { ProductID = productId });
    }

    public async Task<Product?> GetByNameAsync(string productName)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Product>(
            $"{SelectAllColumns} WHERE ProductName = @ProductName COLLATE NOCASE;",
            new { ProductName = productName });
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<Product>(
            $"{SelectAllColumns} ORDER BY ProductName COLLATE NOCASE;");
    }

    public async Task<IEnumerable<Product>> SearchByNameAsync(string partialName)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<Product>(
            $"{SelectAllColumns} WHERE ProductName LIKE @Pattern ESCAPE '\\' COLLATE NOCASE ORDER BY ProductName;",
            new { Pattern = $"%{EscapeLikePattern(partialName)}%" });
    }

    public async Task<IEnumerable<Product>> GetLowStockAsync()
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<Product>(
            $"{SelectAllColumns} WHERE Quantity <= ReorderLevel ORDER BY ProductName COLLATE NOCASE;");
    }

    public async Task<int> AddAsync(Product product)
    {
        await using var connection = await _db.CreateConnectionAsync();

        // INSERT then SELECT last_insert_rowid() in one round trip to get the generated ID.
        return await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO Products (ProductName, Category, Quantity, UnitPrice, ReorderLevel)
            VALUES (@ProductName, @Category, @Quantity, @UnitPrice, @ReorderLevel);
            SELECT last_insert_rowid();
            """,
            new
            {
                product.ProductName,
                product.Category,
                product.Quantity,
                product.UnitPrice,
                product.ReorderLevel
            });
    }

    public async Task UpdateAsync(Product product)
    {
        await using var connection = await _db.CreateConnectionAsync();

        // Quantity is intentionally excluded — stock levels are only mutated
        // through AdjustQuantityAsync to keep the ledger consistent.
        await connection.ExecuteAsync(
            """
            UPDATE Products
            SET ProductName  = @ProductName,
                Category     = @Category,
                UnitPrice    = @UnitPrice,
                ReorderLevel = @ReorderLevel
            WHERE ProductID  = @ProductID;
            """,
            new
            {
                product.ProductName,
                product.Category,
                product.UnitPrice,
                product.ReorderLevel,
                product.ProductID
            });
    }

    public async Task DeleteAsync(int productId)
    {
        await using var connection = await _db.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM Products WHERE ProductID = @ProductID;",
            new { ProductID = productId });
    }

    public async Task AdjustQuantityAsync(
        int productId,
        int delta,
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        await connection.ExecuteAsync(
            "UPDATE Products SET Quantity = Quantity + @Delta WHERE ProductID = @ProductID;",
            new { Delta = delta, ProductID = productId },
            transaction);
    }

    // Escapes LIKE special characters so user input can never act as wildcards.
    private static string EscapeLikePattern(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
