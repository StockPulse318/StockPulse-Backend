using System.Text;
using Dapper;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly DatabaseInitializer _db;

    private const string SelectBase =
        """
        SELECT p.id AS Id,
               p.product_code AS ProductCode,
               p.name AS Name,
               p.category_id AS CategoryId,
               COALESCE(c.name, '') AS CategoryName,
               p.quantity AS Quantity,
               p.unit_price AS UnitPrice,
               p.reorder_level AS ReorderLevel,
               p.created_at AS CreatedAt,
               p.updated_at AS UpdatedAt
        FROM products p
        LEFT JOIN categories c ON p.category_id = c.id
        """;

    public ProductRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Product>(
            $"{SelectBase} WHERE p.id = @Id;",
            new { Id = id });
    }

    public async Task<Product?> GetByCodeAsync(string productCode)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Product>(
            $"{SelectBase} WHERE p.product_code = @ProductCode COLLATE NOCASE;",
            new { ProductCode = productCode.Trim() });
    }

    public async Task<Product?> GetByNameAsync(string name)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Product>(
            $"{SelectBase} WHERE p.name = @Name COLLATE NOCASE;",
            new { Name = name.Trim() });
    }

    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int limit,
        string? sortBy = null,
        string? sortOrder = null,
        int? categoryId = null,
        string? q = null)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);
        var offset = (page - 1) * limit;

        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();

        if (categoryId.HasValue)
        {
            whereClauses.Add("p.category_id = @CategoryId");
            parameters.Add("CategoryId", categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var escaped = EscapeLikePattern(q.Trim());
            whereClauses.Add("(p.name LIKE @Pattern ESCAPE '\\' COLLATE NOCASE OR p.product_code LIKE @Pattern ESCAPE '\\' COLLATE NOCASE)");
            parameters.Add("Pattern", $"%{escaped}%");
        }

        var whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        var orderColumn = (sortBy?.ToLowerInvariant()) switch
        {
            "quantity" => "p.quantity",
            "category" => "c.name COLLATE NOCASE",
            _          => "p.name COLLATE NOCASE"
        };
        var orderDirection = (sortOrder?.ToLowerInvariant() == "desc") ? "DESC" : "ASC";

        var querySql = $"""
            {SelectBase}
            {whereSql}
            ORDER BY {orderColumn} {orderDirection}, p.id ASC
            LIMIT @Limit OFFSET @Offset;
            """;

        var countSql = $"""
            SELECT COUNT(*)
            FROM products p
            LEFT JOIN categories c ON p.category_id = c.id
            {whereSql};
            """;

        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);

        await using var connection = await _db.CreateConnectionAsync();
        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await connection.QueryAsync<Product>(querySql, parameters);

        return (items, totalCount);
    }

    public async Task<IEnumerable<Product>> GetLowStockAsync()
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<Product>(
            $"{SelectBase} WHERE p.quantity <= p.reorder_level ORDER BY p.name COLLATE NOCASE;");
    }

    public async Task<int> AddAsync(Product product)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO products (product_code, name, category_id, quantity, unit_price, reorder_level, created_at, updated_at)
            VALUES (@ProductCode, @Name, @CategoryId, @Quantity, @UnitPrice, @ReorderLevel, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();
            """,
            new
            {
                ProductCode = product.ProductCode.Trim(),
                Name = product.Name.Trim(),
                product.CategoryId,
                product.Quantity,
                product.UnitPrice,
                product.ReorderLevel,
                CreatedAt = string.IsNullOrWhiteSpace(product.CreatedAt) ? DateTime.UtcNow.ToString("o") : product.CreatedAt,
                UpdatedAt = string.IsNullOrWhiteSpace(product.UpdatedAt) ? DateTime.UtcNow.ToString("o") : product.UpdatedAt
            });
    }

    public async Task UpdateAsync(Product product)
    {
        await using var connection = await _db.CreateConnectionAsync();
        await connection.ExecuteAsync(
            """
            UPDATE products
            SET product_code  = @ProductCode,
                name          = @Name,
                category_id   = @CategoryId,
                unit_price    = @UnitPrice,
                reorder_level = @ReorderLevel,
                updated_at    = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE id = @Id;
            """,
            new
            {
                ProductCode = product.ProductCode.Trim(),
                Name = product.Name.Trim(),
                product.CategoryId,
                product.UnitPrice,
                product.ReorderLevel,
                product.Id
            });
    }

    public async Task DeleteAsync(int id)
    {
        await using var connection = await _db.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM products WHERE id = @Id;",
            new { Id = id });
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorMessage)> ExecuteStockInAsync(
        int productId, int amount, int performedByUserId)
    {
        if (amount <= 0)
            return (false, "VALIDATION_ERROR", "Stock-In amount must be a positive integer.");

        await using var connection = await _db.CreateConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var affected = await connection.ExecuteAsync(
            """
            UPDATE products
            SET quantity = quantity + @Amount,
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE id = @ProductId;
            """,
            new { Amount = amount, ProductId = productId },
            transaction);

        if (affected == 0)
        {
            await transaction.RollbackAsync();
            return (false, "NOT_FOUND", $"Product with ID {productId} does not exist.");
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO stock_movements (product_id, type, amount, performed_by, created_at)
            VALUES (@ProductId, 'STOCK_IN', @Amount, @PerformedBy, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            """,
            new { ProductId = productId, Amount = amount, PerformedBy = performedByUserId },
            transaction);

        await transaction.CommitAsync();
        return (true, null, null);
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorMessage)> ExecuteStockOutAsync(
        int productId, int amount, int performedByUserId)
    {
        if (amount <= 0)
            return (false, "VALIDATION_ERROR", "Stock-Out amount must be a positive integer.");

        await using var connection = await _db.CreateConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        // Implement the stock-out check as a single conditional statement executed inside the transaction.
        // Never read the quantity first and write it afterwards.
        var affected = await connection.ExecuteAsync(
            """
            UPDATE products
            SET quantity = quantity - @Amount,
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE id = @ProductId AND quantity >= @Amount;
            """,
            new { Amount = amount, ProductId = productId },
            transaction);

        if (affected == 0)
        {
            await transaction.RollbackAsync();

            // Distinguish whether product exists or stock is insufficient
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM products WHERE id = @ProductId;",
                new { ProductId = productId }) > 0;

            if (!exists)
            {
                return (false, "NOT_FOUND", $"Product with ID {productId} does not exist.");
            }

            return (false, "INSUFFICIENT_STOCK", $"Insufficient stock for product ID {productId} to complete stock-out of {amount} units.");
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO stock_movements (product_id, type, amount, performed_by, created_at)
            VALUES (@ProductId, 'STOCK_OUT', @Amount, @PerformedBy, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            """,
            new { ProductId = productId, Amount = amount, PerformedBy = performedByUserId },
            transaction);

        await transaction.CommitAsync();
        return (true, null, null);
    }

    private static string EscapeLikePattern(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
