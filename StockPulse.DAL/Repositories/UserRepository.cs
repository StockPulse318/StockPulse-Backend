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

    public async Task<User?> GetByUsernameAsync(string username)
    {
        await using var connection = await _db.CreateConnectionAsync();

        return await connection.QuerySingleOrDefaultAsync<User>(
            "SELECT Username, PasswordHash, Role FROM Users WHERE Username = @Username;",
            new { Username = username });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        await using var connection = await _db.CreateConnectionAsync();

        return await connection.QueryAsync<User>(
            "SELECT Username, PasswordHash, Role FROM Users ORDER BY Username;");
    }

    public async Task AddAsync(User user)
    {
        await using var connection = await _db.CreateConnectionAsync();

        await connection.ExecuteAsync(
            "INSERT INTO Users (Username, PasswordHash, Role) VALUES (@Username, @PasswordHash, @Role);",
            new { user.Username, user.PasswordHash, user.Role });
    }

    public async Task DeleteAsync(string username)
    {
        await using var connection = await _db.CreateConnectionAsync();

        await connection.ExecuteAsync(
            "DELETE FROM Users WHERE Username = @Username;",
            new { Username = username });
    }
}
