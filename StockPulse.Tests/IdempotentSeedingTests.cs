using Dapper;
using Microsoft.Extensions.Configuration;
using StockPulse.API.Seeding;
using StockPulse.DAL;
using Xunit;

namespace StockPulse.Tests;

public sealed class IdempotentSeedingTests
{
    [Fact]
    public async Task Running_Seeder_Twice_Produces_No_Duplicates_And_Preserves_Data_Integrity()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"stockpulse_seed_test_{Guid.NewGuid():N}.db");

        try
        {
            var db = new DatabaseInitializer(tempDbPath);
            var config = new ConfigurationBuilder().Build();

            // Run 1: First seed run
            await DatabaseSeeder.SeedAsync(db, config);

            await using (var conn1 = await db.CreateConnectionAsync())
            {
                var userCount1 = await conn1.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users;");
                var catCount1 = await conn1.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM categories;");
                var prodCount1 = await conn1.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products;");
                var moveCount1 = await conn1.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM stock_movements;");

                Assert.Equal(2, userCount1);
                Assert.Equal(7, catCount1);
                Assert.Equal(50, prodCount1);
                Assert.True(moveCount1 > 0);

                // Run 2: Second seed run on same database
                await DatabaseSeeder.SeedAsync(db, config);

                var userCount2 = await conn1.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users;");
                var catCount2 = await conn1.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM categories;");
                var prodCount2 = await conn1.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products;");
                var moveCount2 = await conn1.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM stock_movements;");

                // Verify exact same counts — no duplicates created
                Assert.Equal(userCount1, userCount2);
                Assert.Equal(catCount1, catCount2);
                Assert.Equal(prodCount1, prodCount2);
                Assert.Equal(moveCount1, moveCount2);

                // Verify low-stock proportion is roughly 15% (7 to 9 products out of 50)
                var lowStockCount = await conn1.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM products WHERE quantity <= reorder_level;");
                var percentage = (double)lowStockCount / prodCount1 * 100.0;
                Assert.InRange(percentage, 10.0, 20.0);
            }
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
