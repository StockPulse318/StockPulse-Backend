using Dapper;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly DatabaseInitializer _db;

    public UserRepository(DatabaseInitializer db)
    {
        _db = db;
    }

    private const string SelectAllColumns =
        "SELECT Username, PasswordHash, Role, FullName, AssignedBranch, IsActive, CreatedAt FROM Users";

    public async Task<User?> GetByUsernameAsync(string username)
    {
        await using var connection = await _db.CreateConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<User>(
            $"{SelectAllColumns} WHERE Username = @Username;",
            new { Username = username });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        await using var connection = await _db.CreateConnectionAsync();

        return await connection.QueryAsync<User>(
            $"{SelectAllColumns} ORDER BY Username;");
    }

    public async Task AddAsync(User user)
    {
        await using var connection = await _db.CreateConnectionAsync();

        await connection.ExecuteAsync(
            """
            INSERT INTO Users (Username, PasswordHash, Role, FullName, AssignedBranch, IsActive, CreatedAt)
            VALUES (@Username, @PasswordHash, @Role, @FullName, @AssignedBranch, @IsActive, @CreatedAt);
            """,
            new
            {
                user.Username,
                user.PasswordHash,
                user.Role,
                user.FullName,
                user.AssignedBranch,
                IsActive = user.IsActive ? 1 : 0,
                user.CreatedAt
            });
    }

    public async Task UpdateAsync(User user)
    {
        await using var connection = await _db.CreateConnectionAsync();

        await connection.ExecuteAsync(
            """
            UPDATE Users
            SET FullName       = @FullName,
                Role           = @Role,
                AssignedBranch = @AssignedBranch,
                IsActive       = @IsActive
            WHERE Username     = @Username;
            """,
            new
            {
                user.FullName,
                user.Role,
                user.AssignedBranch,
                IsActive = user.IsActive ? 1 : 0,
                user.Username
            });
    }

    public async Task UpdatePasswordAsync(string username, string passwordHash)
    {
        await using var connection = await _db.CreateConnectionAsync();

        await connection.ExecuteAsync(
            "UPDATE Users SET PasswordHash = @PasswordHash WHERE Username = @Username;",
            new { Username = username, PasswordHash = passwordHash });
    }

    public async Task DeleteAsync(string username)
    {
        await using var connection = await _db.CreateConnectionAsync();

        await connection.ExecuteAsync(
            "DELETE FROM Users WHERE Username = @Username;",
            new { Username = username });
    }
}
