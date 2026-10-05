using Dapper;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly DatabaseInitializer _db;

    public CategoryRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Category>(
            "SELECT id, name FROM categories WHERE id = @Id;",
            new { Id = id });
    }

    public async Task<Category?> GetByNameAsync(string name)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Category>(
            "SELECT id, name FROM categories WHERE name = @Name COLLATE NOCASE;",
            new { Name = name.Trim() });
    }

    public async Task<IEnumerable<Category>> GetAllAsync()
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<Category>(
            "SELECT id, name FROM categories ORDER BY name COLLATE NOCASE;");
    }

    public async Task<int> AddAsync(Category category)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO categories (name) VALUES (@Name);
            SELECT last_insert_rowid();
            """,
            new { Name = category.Name.Trim() });
    }
}
