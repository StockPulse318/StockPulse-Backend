using Dapper;
using Microsoft.Data.Sqlite;

namespace StockPulse.DAL;

/// <summary>
/// Owns the database lifecycle: connection string construction, schema migrations,
/// and the enforcement of SQLite pragmas that must be set per-connection.
///
/// Designed as a singleton dependency — one initializer instance for the application
/// lifetime, with individual repositories opening short-lived connections via
/// CreateConnectionAsync() for each operation.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string databaseFilePath)
    {
        // WAL (Write-Ahead Log) mode dramatically improves concurrent read throughput
        // because readers don't block writers and vice versa — important for an API
        // handling multiple simultaneous requests against the same embedded database.
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    /// <summary>
    /// Returns the connection string for use by repositories.
    /// Kept internal to the DAL — nothing outside this assembly should build raw connections.
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Opens a connection and applies the per-connection pragmas that SQLite requires
    /// to be set on every new connection. Repositories call this instead of managing
    /// SqliteConnection directly so pragma enforcement is never accidentally skipped.
    /// </summary>
    public async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Foreign key enforcement is off by default in SQLite and must be re-enabled
        // for every connection — it does not persist in the database file itself.
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");

        // WAL mode persists in the DB file after first set, but re-asserting it here
        // ensures correct behavior even if the file was created by a different process.
        await connection.ExecuteAsync("PRAGMA journal_mode = WAL;");

        return connection;
    }

    /// <summary>
    /// Runs on application startup. Idempotent — safe to call on every launch.
    /// Uses CREATE TABLE IF NOT EXISTS so it acts as both initial setup and
    /// a lightweight schema guard without a full migration framework.
    /// </summary>
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
                Category     TEXT    NOT NULL,
                Quantity     INTEGER NOT NULL DEFAULT 0 CHECK(Quantity >= 0),
                UnitPrice    REAL    NOT NULL CHECK(UnitPrice > 0),
                ReorderLevel INTEGER NOT NULL CHECK(ReorderLevel >= 0)
            );
            """);

        // ON DELETE CASCADE means removing a Product automatically purges its
        // transaction history — keeps referential integrity without manual cleanup.
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

        // Index on the most common log query paths — prevents full table scans
        // on the logs table as transaction history grows over time.
        await connection.ExecuteAsync("""
            CREATE INDEX IF NOT EXISTS idx_logs_product   ON InventoryTransactionLogs(ProductID);
            CREATE INDEX IF NOT EXISTS idx_logs_handledby ON InventoryTransactionLogs(HandledBy);
            CREATE INDEX IF NOT EXISTS idx_products_name  ON Products(ProductName COLLATE NOCASE);
            """);
    }
}
