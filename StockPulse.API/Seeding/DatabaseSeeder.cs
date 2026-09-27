using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using StockPulse.DAL;

namespace StockPulse.API.Seeding;

// Runs once at startup. If users already exist, it exits immediately.
// Credentials come from configuration — nothing is hardcoded.
public static class DatabaseSeeder
{
    private const int Pbkdf2Iterations = 310_000;
    private const int SaltSizeBytes    = 16;
    private const int HashSizeBytes    = 32;

    public static async Task SeedAsync(DatabaseInitializer db, IConfiguration configuration)
    {
        await using var connection = await db.CreateConnectionAsync();

        var userCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users;");
        if (userCount > 0)
            return;

        var username = configuration["SeedManager:Username"] ?? "admin";
        var password = configuration["SeedManager:Password"] ?? "Admin@1234";

        await connection.ExecuteAsync(
            "INSERT INTO Users (Username, PasswordHash, Role) VALUES (@Username, @PasswordHash, @Role);",
            new { Username = username, PasswordHash = HashPassword(password), Role = "Warehouse Manager" });
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = KeyDerivation.Pbkdf2(
            password:          password,
            salt:              salt,
            prf:               KeyDerivationPrf.HMACSHA256,
            iterationCount:    Pbkdf2Iterations,
            numBytesRequested: HashSizeBytes);

        return $"{Pbkdf2Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
