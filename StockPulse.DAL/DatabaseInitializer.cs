using Dapper;
using Microsoft.Data.Sqlite;

namespace StockPulse.DAL;

public sealed class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string databaseFilePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode       = SqliteOpenMode.ReadWriteCreate,
            Cache      = SqliteCacheMode.Shared
        }.ToString();
    }

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

    // Idempotent — CREATE TABLE IF NOT EXISTS means this is safe to call on every startup.
    public async Task InitializeAsync()
    {
        await using var connection = await CreateConnectionAsync();

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Users (
                Username     TEXT NOT NULL PRIMARY KEY,
                PasswordHash TEXT NOT NULL,
                Role         TEXT NOT NULL CHECK(Role IN ('Warehouse Manager', 'Stock Clerk'))
            );
            """);

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Products (
                ProductID    INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductName  TEXT    NOT NULL UNIQUE,
                Branch       TEXT    NOT NULL DEFAULT 'Main Warehouse',
                Category     TEXT    NOT NULL,
                Quantity     INTEGER NOT NULL DEFAULT 0 CHECK(Quantity >= 0),
                UnitPrice    REAL    NOT NULL CHECK(UnitPrice > 0),
                ReorderLevel INTEGER NOT NULL CHECK(ReorderLevel >= 0)
            );
            """);

        try
        {
            await connection.ExecuteAsync("ALTER TABLE Products ADD COLUMN Branch TEXT NOT NULL DEFAULT 'Main Warehouse';");
        }
        catch
        {
            // Column already exists
        }

        // ON DELETE CASCADE keeps the logs table clean when a product is removed.
        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS InventoryTransactionLogs (
                TransactionID   INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductID       INTEGER NOT NULL,
                TransactionType TEXT    NOT NULL CHECK(TransactionType IN ('Stock-In', 'Stock-Out')),
                QuantityChanged INTEGER NOT NULL,
                HandledBy       TEXT    NOT NULL,
                Timestamp       TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                FOREIGN KEY (ProductID)  REFERENCES Products(ProductID) ON DELETE CASCADE,
                FOREIGN KEY (HandledBy) REFERENCES Users(Username)
            );
            """);

        await connection.ExecuteAsync("""
            CREATE INDEX IF NOT EXISTS idx_logs_product   ON InventoryTransactionLogs(ProductID);
            CREATE INDEX IF NOT EXISTS idx_logs_handledby ON InventoryTransactionLogs(HandledBy);
            CREATE INDEX IF NOT EXISTS idx_products_name  ON Products(ProductName COLLATE NOCASE);
            """);
    }
}
