using Dapper;
using Microsoft.Data.Sqlite;

namespace StockPulse.DAL;

public sealed class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly string _databasePath;

    public DatabaseInitializer(string? databaseFilePath = null)
    {
        var envPath = Environment.GetEnvironmentVariable("DATABASE_PATH");
        var resolvedPath = !string.IsNullOrWhiteSpace(envPath)
            ? envPath
            : (!string.IsNullOrWhiteSpace(databaseFilePath) ? databaseFilePath : "stockpulse.db");

        var dir = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _databasePath = resolvedPath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = resolvedPath,
            Mode       = SqliteOpenMode.ReadWriteCreate,
            Cache      = SqliteCacheMode.Shared
        }.ToString();
    }

    public string DatabasePath => _databasePath;
    public string ConnectionString => _connectionString;

    // Repositories call this instead of constructing connections directly
    // so foreign key enforcement and WAL mode are never accidentally skipped.
    // Both pragmas must be set per-connection — SQLite does not persist them.
    public async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
        await connection.ExecuteAsync("PRAGMA journal_mode = WAL;");
        return connection;
    }

    public async Task InitializeAsync()
    {
        await MigrateAsync();
    }

    public async Task MigrateAsync()
    {
        await using var connection = await CreateConnectionAsync();

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version    INTEGER PRIMARY KEY,
                name       TEXT NOT NULL,
                applied_at TEXT NOT NULL
            );
            """);

        var appliedMigrations = (await connection.QueryAsync<int>(
            "SELECT version FROM schema_migrations;")).ToHashSet();

        if (!appliedMigrations.Contains(1))
        {
            await ApplyMigration001Async(connection);
        }
    }

    private static async Task ApplyMigration001Async(SqliteConnection connection)
    {
        await using var transaction = await connection.BeginTransactionAsync();

        // Check if legacy Products table exists and has Branch column
        var isLegacy = await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM sqlite_master 
            WHERE type='table' AND name='Products';
            """, transaction: transaction) > 0;

        var hasBranch = isLegacy && await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM pragma_table_info('Products') 
            WHERE name='Branch';
            """, transaction: transaction) > 0;

        if (hasBranch)
        {
            // Legacy schema migration:
            // 1. Categories
            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS categories (
                    id   INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL UNIQUE COLLATE NOCASE
                );
                """, transaction: transaction);

            await connection.ExecuteAsync("""
                INSERT OR IGNORE INTO categories (name)
                SELECT DISTINCT 
                    CASE 
                        WHEN INSTR(Category, ' - ') > 0 THEN TRIM(SUBSTR(Category, INSTR(Category, ' - ') + 3))
                        ELSE TRIM(Category) 
                    END
                FROM Products 
                WHERE Category IS NOT NULL AND TRIM(Category) != '';
                """, transaction: transaction);

            // Ensure at least one category exists if legacy was empty
            await connection.ExecuteAsync("""
                INSERT OR IGNORE INTO categories (id, name) VALUES (1, 'General');
                """, transaction: transaction);

            // 2. Users
            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS users_v2 (
                    id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    username      TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    full_name     TEXT NOT NULL DEFAULT '',
                    password_hash TEXT NOT NULL,
                    role          TEXT NOT NULL CHECK(role IN ('WAREHOUSE_MANAGER', 'CLERK')),
                    is_active     INTEGER NOT NULL DEFAULT 1,
                    created_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                );
                """, transaction: transaction);

            await connection.ExecuteAsync("""
                INSERT OR IGNORE INTO users_v2 (username, full_name, password_hash, role, is_active, created_at)
                SELECT Username, COALESCE(FullName, ''), PasswordHash,
                       CASE WHEN Role IN ('Administrator', 'Warehouse Manager', 'WAREHOUSE_MANAGER') THEN 'WAREHOUSE_MANAGER' ELSE 'CLERK' END,
                       COALESCE(IsActive, 1),
                       COALESCE(CreatedAt, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                FROM Users;
                """, transaction: transaction);

            // 3. Products
            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS products_v2 (
                    id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_code  TEXT NOT NULL UNIQUE,
                    name          TEXT NOT NULL,
                    category_id   INTEGER NOT NULL REFERENCES categories(id),
                    quantity      INTEGER NOT NULL DEFAULT 0 CHECK(quantity >= 0),
                    unit_price    REAL NOT NULL CHECK(unit_price >= 0),
                    reorder_level INTEGER NOT NULL CHECK(reorder_level >= 0),
                    created_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                    updated_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                );
                """, transaction: transaction);

            await connection.ExecuteAsync("""
                INSERT OR IGNORE INTO products_v2 (id, product_code, name, category_id, quantity, unit_price, reorder_level, created_at, updated_at)
                SELECT p.ProductID,
                       'PRD-' || substr('0000' || p.ProductID, -4),
                       p.ProductName,
                       COALESCE(c.id, 1),
                       p.Quantity,
                       p.UnitPrice,
                       p.ReorderLevel,
                       strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
                       strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                FROM Products p
                LEFT JOIN categories c ON c.name = (
                    CASE 
                        WHEN INSTR(p.Category, ' - ') > 0 THEN TRIM(SUBSTR(p.Category, INSTR(p.Category, ' - ') + 3))
                        ELSE TRIM(p.Category)
                    END
                );
                """, transaction: transaction);

            // 4. Stock Movements
            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS stock_movements (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_id   INTEGER NOT NULL REFERENCES products_v2(id) ON DELETE CASCADE,
                    type         TEXT NOT NULL CHECK(type IN ('STOCK_IN', 'STOCK_OUT')),
                    amount       INTEGER NOT NULL CHECK(amount > 0),
                    performed_by INTEGER NOT NULL REFERENCES users_v2(id),
                    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                );
                """, transaction: transaction);

            var hasLegacyLogs = await connection.ExecuteScalarAsync<int>("""
                SELECT COUNT(*) FROM sqlite_master 
                WHERE type='table' AND name='InventoryTransactionLogs';
                """, transaction: transaction) > 0;

            if (hasLegacyLogs)
            {
                await connection.ExecuteAsync("""
                    INSERT OR IGNORE INTO stock_movements (id, product_id, type, amount, performed_by, created_at)
                    SELECT l.TransactionID,
                           l.ProductID,
                           CASE WHEN l.TransactionType = 'Stock-In' THEN 'STOCK_IN' ELSE 'STOCK_OUT' END,
                           ABS(l.QuantityChanged),
                           COALESCE(u.id, 1),
                           l.Timestamp
                    FROM InventoryTransactionLogs l
                    LEFT JOIN users_v2 u ON LOWER(u.username) = LOWER(l.HandledBy);
                    """, transaction: transaction);

                await connection.ExecuteAsync("DROP TABLE InventoryTransactionLogs;", transaction: transaction);
            }

            await connection.ExecuteAsync("DROP TABLE Products;", transaction: transaction);
            await connection.ExecuteAsync("ALTER TABLE products_v2 RENAME TO products;", transaction: transaction);

            await connection.ExecuteAsync("DROP TABLE Users;", transaction: transaction);
            await connection.ExecuteAsync("ALTER TABLE users_v2 RENAME TO users;", transaction: transaction);
        }
        else
        {
            // Clean installation or reset
            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS categories (
                    id   INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL UNIQUE COLLATE NOCASE
                );

                CREATE TABLE IF NOT EXISTS users (
                    id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    username      TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    full_name     TEXT NOT NULL DEFAULT '',
                    password_hash TEXT NOT NULL,
                    role          TEXT NOT NULL CHECK(role IN ('WAREHOUSE_MANAGER', 'CLERK')),
                    is_active     INTEGER NOT NULL DEFAULT 1,
                    created_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                );

                CREATE TABLE IF NOT EXISTS products (
                    id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_code  TEXT NOT NULL UNIQUE,
                    name          TEXT NOT NULL,
                    category_id   INTEGER NOT NULL REFERENCES categories(id),
                    quantity      INTEGER NOT NULL DEFAULT 0 CHECK(quantity >= 0),
                    unit_price    REAL NOT NULL CHECK(unit_price >= 0),
                    reorder_level INTEGER NOT NULL CHECK(reorder_level >= 0),
                    created_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                    updated_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                );

                CREATE TABLE IF NOT EXISTS stock_movements (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_id   INTEGER NOT NULL REFERENCES products(id) ON DELETE CASCADE,
                    type         TEXT NOT NULL CHECK(type IN ('STOCK_IN', 'STOCK_OUT')),
                    amount       INTEGER NOT NULL CHECK(amount > 0),
                    performed_by INTEGER NOT NULL REFERENCES users(id),
                    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                );
                """, transaction: transaction);
        }

        // Indexes
        await connection.ExecuteAsync("""
            CREATE INDEX IF NOT EXISTS idx_products_product_code   ON products(product_code);
            CREATE INDEX IF NOT EXISTS idx_products_name           ON products(name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS idx_products_category_id    ON products(category_id);
            CREATE INDEX IF NOT EXISTS idx_stock_movements_product ON stock_movements(product_id);
            CREATE INDEX IF NOT EXISTS idx_stock_movements_user    ON stock_movements(performed_by);
            """, transaction: transaction);

        await connection.ExecuteAsync("""
            INSERT INTO schema_migrations (version, name, applied_at)
            VALUES (1, '001_normalized_schema', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
            """, transaction: transaction);

        await transaction.CommitAsync();
    }

    public async Task ResetAsync()
    {
        await using var connection = await CreateConnectionAsync();
        await connection.ExecuteAsync("PRAGMA foreign_keys = OFF;");
        await connection.ExecuteAsync("""
            DROP TABLE IF EXISTS stock_movements;
            DROP TABLE IF EXISTS InventoryTransactionLogs;
            DROP TABLE IF EXISTS products;
            DROP TABLE IF EXISTS products_v2;
            DROP TABLE IF EXISTS Products;
            DROP TABLE IF EXISTS categories;
            DROP TABLE IF EXISTS users;
            DROP TABLE IF EXISTS users_v2;
            DROP TABLE IF EXISTS Users;
            DROP TABLE IF EXISTS schema_migrations;
            """);
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
        await MigrateAsync();
    }
}
