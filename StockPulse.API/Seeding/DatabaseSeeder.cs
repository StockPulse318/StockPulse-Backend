using Dapper;
using StockPulse.DAL;
using StockPulse.Domain.Entities;

namespace StockPulse.API.Seeding;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(DatabaseInitializer db, IConfiguration configuration)
    {
        await db.MigrateAsync();
        await using var connection = await db.CreateConnectionAsync();

        // 1. Resolve seed user credentials
        var managerUsername = Environment.GetEnvironmentVariable("SEED_MANAGER_USERNAME")
            ?? configuration["SeedManager:Username"]
            ?? "manager";

        var clerkUsername = Environment.GetEnvironmentVariable("SEED_CLERK_USERNAME")
            ?? configuration["SeedClerk:Username"]
            ?? "clerk";

        var defaultPassword = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD")
            ?? configuration["SeedDefaultPassword"]
            ?? "StockPulse@2026";

        Console.WriteLine($"[SEED] Seeding Warehouse Manager: {managerUsername}");
        Console.WriteLine($"[SEED] Seeding Stock Clerk: {clerkUsername}");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

        // Upsert Manager
        await connection.ExecuteAsync("""
            INSERT INTO users (username, full_name, password_hash, role, is_active, created_at)
            VALUES (@Username, @FullName, @PasswordHash, @Role, 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(username) DO UPDATE SET
                full_name = excluded.full_name,
                password_hash = excluded.password_hash,
                role = excluded.role;
            """,
            new
            {
                Username = managerUsername.Trim(),
                FullName = "Warehouse Operations Manager",
                PasswordHash = passwordHash,
                Role = UserRoles.WarehouseManager
            });

        // Upsert Clerk
        await connection.ExecuteAsync("""
            INSERT INTO users (username, full_name, password_hash, role, is_active, created_at)
            VALUES (@Username, @FullName, @PasswordHash, @Role, 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(username) DO UPDATE SET
                full_name = excluded.full_name,
                password_hash = excluded.password_hash,
                role = excluded.role;
            """,
            new
            {
                Username = clerkUsername.Trim(),
                FullName = "Lead Inventory Clerk",
                PasswordHash = passwordHash,
                Role = UserRoles.Clerk
            });

        var managerId = await connection.ExecuteScalarAsync<int>(
            "SELECT id FROM users WHERE username = @Username;", new { Username = managerUsername.Trim() });
        var clerkId = await connection.ExecuteScalarAsync<int>(
            "SELECT id FROM users WHERE username = @Username;", new { Username = clerkUsername.Trim() });

        // 2. Seed Categories (7 distinct, clean categories)
        var categories = new[]
        {
            "Building & Construction",
            "Electrical & Power",
            "Hardware & Tools",
            "Plumbing & Drainage",
            "Paints & Protective Coatings",
            "Safety & Security Gear",
            "Warehouse & Material Handling"
        };

        foreach (var categoryName in categories)
        {
            await connection.ExecuteAsync("""
                INSERT INTO categories (name) VALUES (@Name)
                ON CONFLICT(name) DO NOTHING;
                """,
                new { Name = categoryName });
        }

        var categoryMap = (await connection.QueryAsync<(int Id, string Name)>(
            "SELECT id, name FROM categories;"))
            .ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

        // 3. Seed 50 realistic products with Ghana cedis pricing
        // ~8 out of 50 products (16%) are low-stock (Quantity <= ReorderLevel)
        var products = new[]
        {
            // Building & Construction
            new { Code = "PRD-1001", Name = "Portland Cement 50kg Grade 42.5N", Cat = "Building & Construction", Qty = 180, Price = 88.50m, Reorder = 50 },
            new { Code = "PRD-1002", Name = "High-Tensile Iron Rods 12mm x 12m", Cat = "Building & Construction", Qty = 45, Price = 135.00m, Reorder = 40 },
            new { Code = "PRD-1003", Name = "Solid Sandcrete Blocks 5-inch", Cat = "Building & Construction", Qty = 500, Price = 9.50m, Reorder = 150 },
            new { Code = "PRD-1004", Name = "Galvanized Binding Wire 25kg Roll", Cat = "Building & Construction", Qty = 30, Price = 210.00m, Reorder = 15 },
            new { Code = "PRD-1005", Name = "Aluzinc Corrugated Roofing Sheet 3m x 0.4mm", Cat = "Building & Construction", Qty = 40, Price = 175.00m, Reorder = 50 }, // Low stock (40 <= 50)
            new { Code = "PRD-1006", Name = "Seasoned Hardwood Timber 2x4 12ft", Cat = "Building & Construction", Qty = 110, Price = 48.00m, Reorder = 40 },
            new { Code = "PRD-1007", Name = "Marine Plywood Sheet 18mm 4x8ft", Cat = "Building & Construction", Qty = 60, Price = 380.00m, Reorder = 20 },

            // Electrical & Power
            new { Code = "PRD-1008", Name = "Copper Cable 2.5mm Roll (100m)", Cat = "Electrical & Power", Qty = 8, Price = 320.00m, Reorder = 15 }, // Low stock (8 <= 15)
            new { Code = "PRD-1009", Name = "Schneider Circuit Breaker 63A Double Pole", Cat = "Electrical & Power", Qty = 35, Price = 55.00m, Reorder = 20 },
            new { Code = "PRD-1010", Name = "Heavy Duty PVC Conduit Pipe 20mm x 3m", Cat = "Electrical & Power", Qty = 120, Price = 18.00m, Reorder = 60 },
            new { Code = "PRD-1011", Name = "Industrial Extension Reel 50m Heavy Duty", Cat = "Electrical & Power", Qty = 14, Price = 380.00m, Reorder = 10 },
            new { Code = "PRD-1012", Name = "Copper Earth Rod 5/8in x 4ft with Clamp", Cat = "Electrical & Power", Qty = 50, Price = 65.00m, Reorder = 25 },
            new { Code = "PRD-1013", Name = "LED High Bay Industrial Lamp 150W", Cat = "Electrical & Power", Qty = 25, Price = 280.00m, Reorder = 10 },
            new { Code = "PRD-1014", Name = "Armoured Underground Cable 4-Core 16mm", Cat = "Electrical & Power", Qty = 5, Price = 850.00m, Reorder = 12 }, // Low stock (5 <= 12)

            // Hardware & Tools
            new { Code = "PRD-1015", Name = "Viro Solid Brass Padlock 70mm", Cat = "Hardware & Tools", Qty = 65, Price = 75.00m, Reorder = 25 },
            new { Code = "PRD-1016", Name = "Bosch Professional Angle Grinder 9in 2200W", Cat = "Hardware & Tools", Qty = 18, Price = 380.00m, Reorder = 10 },
            new { Code = "PRD-1017", Name = "Makita Rotary Hammer Drill 800W", Cat = "Hardware & Tools", Qty = 12, Price = 520.00m, Reorder = 8 },
            new { Code = "PRD-1018", Name = "Drop Forged Claw Hammer 16oz Fiberglass", Cat = "Hardware & Tools", Qty = 80, Price = 42.00m, Reorder = 30 },
            new { Code = "PRD-1019", Name = "Adjustable Heavy Duty Pipe Wrench 18in", Cat = "Hardware & Tools", Qty = 22, Price = 95.00m, Reorder = 15 },
            new { Code = "PRD-1020", Name = "Stainless Steel Hex Wood Screws Box (500pcs)", Cat = "Hardware & Tools", Qty = 140, Price = 35.00m, Reorder = 50 },
            new { Code = "PRD-1021", Name = "Stanley Heavy Duty Tape Measure 8m", Cat = "Hardware & Tools", Qty = 90, Price = 38.00m, Reorder = 30 },

            // Plumbing & Drainage
            new { Code = "PRD-1022", Name = "PVC Pressure Pipe Class E 4in x 6m", Cat = "Plumbing & Drainage", Qty = 75, Price = 68.00m, Reorder = 30 },
            new { Code = "PRD-1023", Name = "Solid Brass Gate Valve 2in Female Thread", Cat = "Plumbing & Drainage", Qty = 4, Price = 95.00m, Reorder = 10 }, // Low stock (4 <= 10)
            new { Code = "PRD-1024", Name = "Polyethylene Water Tank 1000 Litres", Cat = "Plumbing & Drainage", Qty = 6, Price = 1350.00m, Reorder = 8 }, // Low stock (6 <= 8)
            new { Code = "PRD-1025", Name = "Submersible Deep Well Water Pump 1.5HP", Cat = "Plumbing & Drainage", Qty = 10, Price = 1050.00m, Reorder = 5 },
            new { Code = "PRD-1026", Name = "HDPE Pipe Roll 32mm PN16 (100m)", Cat = "Plumbing & Drainage", Qty = 18, Price = 460.00m, Reorder = 10 },
            new { Code = "PRD-1027", Name = "Brass Float Valve for Water Tank 1in", Cat = "Plumbing & Drainage", Qty = 45, Price = 58.00m, Reorder = 20 },
            new { Code = "PRD-1028", Name = "PVC Solvent Cement Glue 500ml Can", Cat = "Plumbing & Drainage", Qty = 85, Price = 32.00m, Reorder = 30 },

            // Paints & Protective Coatings
            new { Code = "PRD-1029", Name = "Anti-Corrosive Marine Paint 20L Grey", Cat = "Paints & Protective Coatings", Qty = 14, Price = 580.00m, Reorder = 20 }, // Low stock (14 <= 20)
            new { Code = "PRD-1030", Name = "Two-Pack Epoxy Floor Primer 5L", Cat = "Paints & Protective Coatings", Qty = 32, Price = 165.00m, Reorder = 15 },
            new { Code = "PRD-1031", Name = "Gloss Enamel Exterior Paint 20L Brilliant White", Cat = "Paints & Protective Coatings", Qty = 40, Price = 420.00m, Reorder = 20 },
            new { Code = "PRD-1032", Name = "Bituminous Waterproofing Membrane 10m Roll", Cat = "Paints & Protective Coatings", Qty = 25, Price = 240.00m, Reorder = 15 },
            new { Code = "PRD-1033", Name = "Mineral Turpentine Paint Thinner 5L", Cat = "Paints & Protective Coatings", Qty = 70, Price = 45.00m, Reorder = 25 },
            new { Code = "PRD-1034", Name = "Heavy Duty Roller Paint Applicator 9in", Cat = "Paints & Protective Coatings", Qty = 110, Price = 25.00m, Reorder = 40 },
            new { Code = "PRD-1035", Name = "Industrial Zinc Phosphate Primer 20L Red Oxide", Cat = "Paints & Protective Coatings", Qty = 22, Price = 390.00m, Reorder = 15 },

            // Safety & Security Gear
            new { Code = "PRD-1036", Name = "Heavy Duty Steel Toe Work Boots (Size 43)", Cat = "Safety & Security Gear", Qty = 85, Price = 220.00m, Reorder = 30 },
            new { Code = "PRD-1037", Name = "High-Visibility Safety Vest with Pockets", Cat = "Safety & Security Gear", Qty = 160, Price = 32.00m, Reorder = 50 },
            new { Code = "PRD-1038", Name = "Industrial Safety Helmet with Face Shield", Cat = "Safety & Security Gear", Qty = 0, Price = 65.00m, Reorder = 25 }, // Low stock / Out of stock (0 <= 25)
            new { Code = "PRD-1039", Name = "Chemical Splash Safety Goggles UV Rated", Cat = "Safety & Security Gear", Qty = 130, Price = 24.00m, Reorder = 40 },
            new { Code = "PRD-1040", Name = "Heavy Duty Nitrile Chemical Gloves Pack (12 pairs)", Cat = "Safety & Security Gear", Qty = 95, Price = 85.00m, Reorder = 35 },
            new { Code = "PRD-1041", Name = "Full Body Safety Harness with Double Lanyard", Cat = "Safety & Security Gear", Qty = 20, Price = 340.00m, Reorder = 10 },
            new { Code = "PRD-1042", Name = "Industrial First Aid Kit Wall Mounted (50 Persons)", Cat = "Safety & Security Gear", Qty = 15, Price = 260.00m, Reorder = 8 },

            // Warehouse & Material Handling
            new { Code = "PRD-1043", Name = "Heavy Duty Pallet Hand Truck 2.5 Ton", Cat = "Warehouse & Material Handling", Qty = 7, Price = 1950.00m, Reorder = 3 },
            new { Code = "PRD-1044", Name = "Heavy Duty Cargo Lashing Belts 5 Ton 9m", Cat = "Warehouse & Material Handling", Qty = 70, Price = 110.00m, Reorder = 20 },
            new { Code = "PRD-1045", Name = "Hydraulic Bottle Jack 20 Ton Industrial", Cat = "Warehouse & Material Handling", Qty = 5, Price = 690.00m, Reorder = 5 }, // Exact boundary (5 == 5)
            new { Code = "PRD-1046", Name = "Industrial Ratchet Tie-Down Straps 50mm x 10m", Cat = "Warehouse & Material Handling", Qty = 90, Price = 45.00m, Reorder = 30 },
            new { Code = "PRD-1047", Name = "Plastic Euro Storage Pallet 1200x800mm", Cat = "Warehouse & Material Handling", Qty = 120, Price = 140.00m, Reorder = 40 },
            new { Code = "PRD-1048", Name = "Heavy Duty Warehouse Platform Trolley 300kg", Cat = "Warehouse & Material Handling", Qty = 16, Price = 480.00m, Reorder = 8 },
            new { Code = "PRD-1049", Name = "Stretch Wrap Film Roll 500mm x 300m Industrial", Cat = "Warehouse & Material Handling", Qty = 85, Price = 65.00m, Reorder = 30 },
            new { Code = "PRD-1050", Name = "Electric Chain Hoist 1 Ton with Remote", Cat = "Warehouse & Material Handling", Qty = 4, Price = 3200.00m, Reorder = 2 }
        };

        foreach (var p in products)
        {
            var categoryId = categoryMap[p.Cat];

            await connection.ExecuteAsync("""
                INSERT INTO products (product_code, name, category_id, quantity, unit_price, reorder_level, created_at, updated_at)
                VALUES (@Code, @Name, @CategoryId, @Qty, @Price, @Reorder, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'), strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                ON CONFLICT(product_code) DO UPDATE SET
                    name = excluded.name,
                    category_id = excluded.category_id,
                    quantity = excluded.quantity,
                    unit_price = excluded.unit_price,
                    reorder_level = excluded.reorder_level,
                    updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now');
                """,
                new
                {
                    p.Code,
                    p.Name,
                    CategoryId = categoryId,
                    p.Qty,
                    p.Price,
                    p.Reorder
                });

            var productId = await connection.ExecuteScalarAsync<int>(
                "SELECT id FROM products WHERE product_code = @Code;", new { p.Code });

            // 4. Modest initial stock movement history (idempotent: only if no movements exist for this product)
            var movementCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM stock_movements WHERE product_id = @ProductId;", new { ProductId = productId });

            if (movementCount == 0 && p.Qty > 0)
            {
                await connection.ExecuteAsync("""
                    INSERT INTO stock_movements (product_id, type, amount, performed_by, created_at)
                    VALUES (@ProductId, 'STOCK_IN', @Amount, @PerformedBy, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
                    """,
                    new
                    {
                        ProductId = productId,
                        Amount = p.Qty,
                        PerformedBy = managerId
                    });
            }
        }

        Console.WriteLine($"[SEED] Seeding completed: 2 users, {categories.Length} categories, {products.Length} products.");
    }
}
