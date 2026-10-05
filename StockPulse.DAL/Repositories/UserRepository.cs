using Dapper;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly DatabaseInitializer _db;

    private const string SelectColumns =
        "SELECT id AS Id, username AS Username, full_name AS FullName, password_hash AS PasswordHash, role AS Role, is_active AS IsActive, created_at AS CreatedAt FROM users";

    public UserRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<User>(
            $"{SelectColumns} WHERE id = @Id;",
            new { Id = id });
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<User>(
            $"{SelectColumns} WHERE username = @Username COLLATE NOCASE;",
            new { Username = username.Trim() });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.QueryAsync<User>(
            $"{SelectColumns} ORDER BY username COLLATE NOCASE;");
    }

    public async Task<int> AddAsync(User user)
    {
        await using var connection = await _db.CreateConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO users (username, full_name, password_hash, role, is_active, created_at)
            VALUES (@Username, @FullName, @PasswordHash, @Role, @IsActive, @CreatedAt);
            SELECT last_insert_rowid();
            """,
            new
            {
                Username = user.Username.Trim(),
                FullName = user.FullName.Trim(),
                user.PasswordHash,
                user.Role,
                IsActive = user.IsActive ? 1 : 0,
                user.CreatedAt
            });
    }

    public async Task UpdateAsync(User user)
    {
        await using var connection = await _db.CreateConnectionAsync();
        await connection.ExecuteAsync(
            """
            UPDATE users
            SET full_name     = @FullName,
                password_hash = @PasswordHash,
                role          = @Role,
                is_active     = @IsActive
            WHERE id = @Id;
            """,
            new
            {
                FullName = user.FullName.Trim(),
                user.PasswordHash,
                user.Role,
                IsActive = user.IsActive ? 1 : 0,
                user.Id
            });
    }

    public async Task DeleteAsync(int id)
    {
        await using var connection = await _db.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM users WHERE id = @Id;",
            new { Id = id });
    }
}
