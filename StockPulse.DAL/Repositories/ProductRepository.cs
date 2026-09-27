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

        // The '%' wildcards are applied here, not by the caller, so the BLL
        // never has to know how partial matching is physically implemented.
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

        // Dapper's ExecuteScalarAsync captures the AUTOINCREMENT value in one round trip.
        var newId = await connection.ExecuteScalarAsync<int>(
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

        return newId;
    }

    public async Task UpdateAsync(Product product)
    {
        await using var connection = await _db.CreateConnectionAsync();

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

        // Quantity is intentionally excluded — stock levels are only mutated
        // through the atomic AdjustQuantityAsync path to preserve ledger integrity.
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
        // The CHECK(Quantity >= 0) constraint on the DB column acts as a final
        // safety net, but the BLL pre-validates stock availability before calling
        // here so the constraint should never be the first line of defense.
        await connection.ExecuteAsync(
            "UPDATE Products SET Quantity = Quantity + @Delta WHERE ProductID = @ProductID;",
            new { Delta = delta, ProductID = productId },
            transaction);
    }

    /// <summary>
    /// Escapes LIKE special characters in user-supplied search input so that
    /// literal '%' or '_' characters in product names don't accidentally act
    /// as wildcards and return unintended results.
    /// </summary>
    private static string EscapeLikePattern(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
