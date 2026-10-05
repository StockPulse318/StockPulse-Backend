using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using StockPulse.DAL;

namespace StockPulse.API.Seeding;

/// <summary>
/// Seeds the database on first run with:
/// - Accounts across warehouse branches (Warehouse Managers and Stock Clerks)
/// - Inventory products categorized across multiple warehouse branches (Accra, Tema, Kumasi, Takoradi)
/// - Initial transaction audit ledger logs
/// </summary>
public static class DatabaseSeeder
{
    private const int Pbkdf2Iterations = 310_000;
    private const int SaltSizeBytes    = 16;
    private const int HashSizeBytes    = 32;

    public static async Task SeedAsync(DatabaseInitializer db, IConfiguration configuration)
    {
        await using var connection = await db.CreateConnectionAsync();

        // 1. Seed Users (Ensure all branch administrators, managers, and clerks exist)
        var seedUsers = new[]
        {
            // Central / Head Office
            new { Username = configuration["SeedManager:Username"] ?? "admin", Password = configuration["SeedManager:Password"] ?? "Admin@1234", Role = "Administrator", FullName = "System Administrator", AssignedBranch = "All Branches" },
            new { Username = "manager", Password = "Manager@1234", Role = "Warehouse Manager", FullName = "National Operations Lead", AssignedBranch = "All Branches" },
            new { Username = "clerk", Password = "Clerk@1234", Role = "Stock Clerk", FullName = "General Floating Clerk", AssignedBranch = "All Branches" },

            // Accra Central Warehouse
            new { Username = "manager_accra", Password = "Manager@1234", Role = "Warehouse Manager", FullName = "Kwame Mensah", AssignedBranch = "Accra Central" },
            new { Username = "clerk_accra", Password = "Clerk@1234", Role = "Stock Clerk", FullName = "Emmanuel Addo", AssignedBranch = "Accra Central" },
            new { Username = "kofi_mensah", Password = "Clerk@1234", Role = "Stock Clerk", FullName = "Kofi Mensah Jr.", AssignedBranch = "Accra Central" },

            // Tema Harbor Depot
            new { Username = "manager_tema", Password = "Manager@1234", Role = "Warehouse Manager", FullName = "Abena Osei", AssignedBranch = "Tema Harbor" },
            new { Username = "clerk_tema", Password = "Clerk@1234", Role = "Stock Clerk", FullName = "Samuel Annan", AssignedBranch = "Tema Harbor" },
            new { Username = "ama_boateng", Password = "Clerk@1234", Role = "Stock Clerk", FullName = "Ama Boateng", AssignedBranch = "Tema Harbor" },

            // Kumasi Regional Branch
            new { Username = "manager_kumasi", Password = "Manager@1234", Role = "Warehouse Manager", FullName = "Yaw Frimpong", AssignedBranch = "Kumasi Depot" },
            new { Username = "clerk_kumasi", Password = "Clerk@1234", Role = "Stock Clerk", FullName = "Akosua Serwaa", AssignedBranch = "Kumasi Depot" },

            // Takoradi Logistics Branch
            new { Username = "manager_takoradi", Password = "Manager@1234", Role = "Warehouse Manager", FullName = "Ebenezer Quaye", AssignedBranch = "Takoradi Logistics" },
            new { Username = "clerk_takoradi", Password = "Clerk@1234", Role = "Stock Clerk", FullName = "Grace Tandoh", AssignedBranch = "Takoradi Logistics" }
        };

        foreach (var u in seedUsers)
        {
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = LOWER(@Username);", new { u.Username });

            if (exists == 0)
            {
                await connection.ExecuteAsync(
                    """
                    INSERT INTO Users (Username, PasswordHash, Role, FullName, AssignedBranch, IsActive)
                    VALUES (@Username, @PasswordHash, @Role, @FullName, @AssignedBranch, 1);
                    """,
                    new { u.Username, PasswordHash = HashPassword(u.Password), u.Role, u.FullName, u.AssignedBranch });
            }
            else
            {
                await connection.ExecuteAsync(
                    """
                    UPDATE Users 
                    SET FullName = COALESCE(NULLIF(FullName, ''), @FullName),
                        AssignedBranch = COALESCE(NULLIF(AssignedBranch, ''), @AssignedBranch),
                        Role = CASE WHEN LOWER(Username) = 'admin' THEN 'Administrator' ELSE Role END
                    WHERE LOWER(Username) = LOWER(@Username);
                    """,
                    new { u.Username, u.FullName, u.AssignedBranch });
            }
        }

        // 2. Seed Products across Warehouse Branches
        var seedProducts = new[]
        {
            // Accra Central Warehouse
            new { Name = "Portland Cement 50kg Grade 42.5N", Category = "Accra Central - Building Supplies", Qty = 180, Price = 78.50m, Reorder = 50 },
            new { Name = "High-Tensile Iron Rods 12mm x 12m", Category = "Accra Central - Building Supplies", Qty = 45, Price = 125.00m, Reorder = 40 },
            new { Name = "Solid Sandcrete Blocks 5-inch", Category = "Accra Central - Building Supplies", Qty = 500, Price = 8.50m, Reorder = 150 },
            new { Name = "Copper Cable 2.5mm Roll (100m)", Category = "Accra Central - Electrical & Power", Qty = 8, Price = 280.00m, Reorder = 15 }, // Low stock
            new { Name = "Schneider Circuit Breaker 63A Double Pole", Category = "Accra Central - Electrical & Power", Qty = 35, Price = 45.00m, Reorder = 20 },
            new { Name = "Heavy Duty PVC Conduit Pipe 20mm x 3m", Category = "Accra Central - Electrical & Power", Qty = 120, Price = 16.00m, Reorder = 60 },
            new { Name = "Industrial Extension Reel 50m Heavy Duty", Category = "Accra Central - Electrical & Power", Qty = 14, Price = 340.00m, Reorder = 10 },

            // Tema Harbor Depot
            new { Name = "Viro Solid Brass Padlock 70mm", Category = "Tema Harbor - Hardware & Security", Qty = 60, Price = 65.00m, Reorder = 25 },
            new { Name = "Galvanized Steel Wire Rope 10mm (per meter)", Category = "Tema Harbor - Heavy Rigging", Qty = 12, Price = 420.00m, Reorder = 20 }, // Low stock
            new { Name = "Heavy Duty Steel Toe Work Boots (Size 43)", Category = "Tema Harbor - Safety Gear", Qty = 85, Price = 195.00m, Reorder = 30 },
            new { Name = "High-Visibility Safety Vest with Pockets", Category = "Tema Harbor - Safety Gear", Qty = 150, Price = 28.00m, Reorder = 50 },
            new { Name = "Anti-Corrosive Marine Paint 20L Grey", Category = "Tema Harbor - Paints & Protective Coatings", Qty = 14, Price = 520.00m, Reorder = 20 }, // Low stock
            new { Name = "Two-Pack Epoxy Floor Primer 5L", Category = "Tema Harbor - Paints & Protective Coatings", Qty = 30, Price = 145.00m, Reorder = 15 },
            new { Name = "Heavy Duty Cargo Lashing Belts 5 Ton 9m", Category = "Tema Harbor - Shipping & Storage", Qty = 65, Price = 95.00m, Reorder = 20 },

            // Kumasi Regional Depot
            new { Name = "PVC Pressure Pipe Class E 4in x 6m", Category = "Kumasi Depot - Plumbing & Drainage", Qty = 75, Price = 58.00m, Reorder = 30 },
            new { Name = "Solid Brass Gate Valve 2in Female Thread", Category = "Kumasi Depot - Plumbing & Drainage", Qty = 4, Price = 85.00m, Reorder = 10 }, // Low stock
            new { Name = "Polyethylene Water Tank 1000 Litres", Category = "Kumasi Depot - Water Storage", Qty = 6, Price = 1250.00m, Reorder = 8 }, // Low stock
            new { Name = "Submersible Deep Well Water Pump 1.5HP", Category = "Kumasi Depot - Pumps & Machinery", Qty = 10, Price = 920.00m, Reorder = 5 },
            new { Name = "Aluzinc Corrugated Roofing Sheet 3m x 0.4mm", Category = "Kumasi Depot - Roofing & Timber", Qty = 40, Price = 160.00m, Reorder = 50 }, // Low stock
            new { Name = "Seasoned Hardwood Timber 2x4 12ft (per piece)", Category = "Kumasi Depot - Roofing & Timber", Qty = 110, Price = 42.00m, Reorder = 40 },
            new { Name = "Roofing Screws with Rubber Washer 65mm (Pack of 100)", Category = "Kumasi Depot - Fasteners", Qty = 95, Price = 35.00m, Reorder = 30 },

            // Takoradi Logistics Branch
            new { Name = "Hydraulic Bottle Jack 20 Ton Industrial", Category = "Takoradi Logistics - Heavy Equipment", Qty = 5, Price = 650.00m, Reorder = 5 },
            new { Name = "Industrial Ratchet Tie-Down Straps 50mm x 10m", Category = "Takoradi Logistics - Cargo Handling", Qty = 90, Price = 38.00m, Reorder = 30 },
            new { Name = "Industrial Safety Helmet with Face Shield", Category = "Takoradi Logistics - Safety Gear", Qty = 0, Price = 55.00m, Reorder = 25 }, // Out of stock
            new { Name = "Bosch Professional Angle Grinder 9in 2200W", Category = "Takoradi Logistics - Power Tools", Qty = 18, Price = 340.00m, Reorder = 12 },
            new { Name = "Grade 304 Stainless Steel Wood Screws Box (500pcs)", Category = "Takoradi Logistics - Fasteners", Qty = 200, Price = 22.00m, Reorder = 50 },
            new { Name = "Heavy Duty Pallet Hand Truck 2.5 Ton", Category = "Takoradi Logistics - Warehouse Equipment", Qty = 7, Price = 1850.00m, Reorder = 3 }
        };

        foreach (var p in seedProducts)
        {
            var branch = "Main Warehouse";
            var category = p.Category;
            if (p.Category.Contains(" - "))
            {
                var parts = p.Category.Split(" - ", 2);
                branch = parts[0].Trim();
                category = parts[1].Trim();
            }

            var prodExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Products WHERE ProductName = @Name AND Branch = @Branch;",
                new { p.Name, Branch = branch });

            if (prodExists == 0)
            {
                var newId = await connection.ExecuteScalarAsync<int>("""
                    INSERT INTO Products (ProductName, Branch, Category, Quantity, UnitPrice, ReorderLevel)
                    VALUES (@Name, @Branch, @Category, @Qty, @Price, @Reorder);
                    SELECT last_insert_rowid();
                    """, new { p.Name, Branch = branch, Category = category, p.Qty, p.Price, p.Reorder });

                // Initial Stock-In transaction log entry
                await connection.ExecuteAsync("""
                    INSERT INTO InventoryTransactionLogs (ProductID, TransactionType, QuantityChanged, HandledBy, Timestamp)
                    VALUES (@ProductID, 'Stock-In', @Qty, 'manager_accra', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
                    """, new { ProductID = newId, p.Qty });
            }
        }
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
