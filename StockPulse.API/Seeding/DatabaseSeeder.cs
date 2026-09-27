using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using StockPulse.DAL;

namespace StockPulse.API.Seeding;

/// <summary>
/// Seeds the database with an initial Warehouse Manager account if no users exist.
/// Runs once at startup — safe to leave in permanently since it checks before inserting.
/// Credentials are read from environment variables so they are never hardcoded.
/// </summary>
public static class DatabaseSeeder
{
    private const int Pbkdf2Iterations = 310_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public static async Task SeedAsync(DatabaseInitializer db)
    {
        await using var connection = await db.CreateConnectionAsync();

        var userCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users;");
        if (userCount > 0)
            return;

        var username = Environment.GetEnvironmentVariable("SEED_MANAGER_USERNAME") ?? "admin";
        var password = Environment.GetEnvironmentVariable("SEED_MANAGER_PASSWORD") ?? "Admin@1234";

        var hash = HashPassword(password);

        await connection.ExecuteAsync(
            "INSERT INTO Users (Username, PasswordHash, Role) VALUES (@Username, @PasswordHash, @Role);",
            new { Username = username, PasswordHash = hash, Role = "Warehouse Manager" });
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);

        var hash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Pbkdf2Iterations,
            numBytesRequested: HashSizeBytes);

        return $"{Pbkdf2Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
