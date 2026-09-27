# StockPulse Backend

> Backend for a desktop Warehouse Inventory Management System — covering data access, business logic, authentication, and role enforcement.

---

## Overview

StockPulse Backend is the data and business logic foundation of the StockPulse Warehouse Management System. It provides a fully async, thread-safe, and role-governed backend stack designed to plug directly into a WPF desktop frontend.

The stack is deliberately lightweight — no ORM bloat, no runtime framework overhead. Raw SQL through Dapper, ACID-compliant transactions enforced at the service layer, and PBKDF2-secured authentication with zero third-party auth dependencies.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# / .NET 8 |
| Database | SQLite (embedded) |
| Data Access | Dapper + Microsoft.Data.Sqlite |

| Auth / Security | PBKDF2-HMAC-SHA256 (310,000 iterations) |
| Dependency Injection | Microsoft.Extensions.DependencyInjection |

---

## Project Structure

```
StockPulse-Backend/
├── StockPulse.sln
│
├── StockPulse.Domain/               # Pure domain — zero infrastructure dependencies
│   ├── Entities/
│   │   ├── User.cs
│   │   ├── Product.cs
│   │   └── InventoryTransactionLog.cs
│   ├── Exceptions/
│   │   ├── InsufficientStockException.cs
│   │   ├── DuplicateProductException.cs
│   │   └── UnauthorizedActionException.cs
│   └── Results/
│       └── Result.cs
│
├── StockPulse.DAL/                  # Database I/O — Dapper, schema, migrations
│   ├── DatabaseInitializer.cs
│   ├── Interfaces/
│   │   ├── IUserRepository.cs
│   │   ├── IProductRepository.cs
│   │   └── ITransactionLogRepository.cs
│   └── Repositories/
│       ├── UserRepository.cs
│       ├── ProductRepository.cs
│       └── TransactionLogRepository.cs
│
└── StockPulse.BLL/                  # Business rules, RBAC, transactional boundaries
    ├── Extensions/
    │   └── ServiceCollectionExtensions.cs
    ├── Interfaces/
    │   ├── IAuthService.cs
    │   ├── IProductService.cs
    │   └── ITransactionService.cs
    └── Services/
        ├── AuthService.cs
        ├── ProductService.cs
        └── TransactionService.cs
```

---

## Database Schema

SQLite with `PRAGMA foreign_keys = ON` and `PRAGMA journal_mode = WAL` enforced on every connection.

### Users
| Column | Type | Constraints |
|---|---|---|
| Username | TEXT | Primary Key |
| PasswordHash | TEXT | Not Null |
| Role | TEXT | Not Null — `'Warehouse Manager'` or `'Stock Clerk'` |

### Products
| Column | Type | Constraints |
|---|---|---|
| ProductID | INTEGER | Primary Key, Autoincrement |
| ProductName | TEXT | Not Null, Unique |
| Category | TEXT | Not Null |
| Quantity | INTEGER | Not Null, Default 0, `>= 0` |
| UnitPrice | REAL | Not Null, `> 0` |
| ReorderLevel | INTEGER | Not Null, `>= 0` |

### InventoryTransactionLogs
| Column | Type | Constraints |
|---|---|---|
| TransactionID | INTEGER | Primary Key, Autoincrement |
| ProductID | INTEGER | FK → Products(ProductID) ON DELETE CASCADE |
| TransactionType | TEXT | Not Null — `'Stock-In'` or `'Stock-Out'` |
| QuantityChanged | INTEGER | Not Null |
| HandledBy | TEXT | FK → Users(Username) |
| Timestamp | TEXT | Not Null, Default `strftime('%Y-%m-%dT%H:%M:%fZ', 'now')` |

---

## Key Features

### Atomic Stock Movements
Stock-In and Stock-Out operations open a single SQLite connection, begin an explicit transaction, and execute the quantity update and audit log insert as one atomic unit. If either statement fails, the entire transaction is rolled back — ensuring stock levels and the transaction ledger are always consistent with each other.

### Role-Based Access Control
Two roles are enforced at the service layer before any database call is made:

| Operation | Warehouse Manager | Stock Clerk |
|---|---|---|
| View products & search | ✅ | ✅ |
| Stock-In / Stock-Out | ✅ | ✅ |
| View transaction logs | ✅ | Own logs only |
| Create / Update / Delete products | ✅ | ❌ |
| Manage users | ✅ | ❌ |

Violations raise a typed `UnauthorizedActionException` that is surfaced to the caller as a `Result.Failure` — no raw exceptions leak to the UI layer.

### Result-Typed API
Every service method returns a `Result<T>` or `Result` discriminated union instead of throwing exceptions at the call boundary. Callers are forced to explicitly handle both success and failure paths.

```csharp
var result = await productService.AddProductAsync(actorUsername, product);

if (result.IsSuccess)
    Console.WriteLine($"Created with ID: {result.Value}");
else
    Console.WriteLine($"Error: {result.ErrorMessage}");
```

### Password Security
Passwords are stored using PBKDF2-HMAC-SHA256 with a 128-bit cryptographically random salt and 310,000 iterations (aligned with OWASP 2023 recommendations). The stored hash is self-describing — it embeds the iteration count so future tuning does not break existing accounts. Verification uses constant-time byte comparison to prevent timing side-channel attacks.

### Low Stock Detection
`Product.IsLowStock` is a computed property (`Quantity <= ReorderLevel`) evaluated inline from values already fetched — no extra query, always consistent with the snapshot read from the database.

### SQL Injection Prevention
All queries use Dapper parameterization exclusively. `LIKE` wildcard characters in user-supplied search input are escaped server-side before the pattern is passed to SQLite.

---

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- JetBrains Rider (recommended) or any .NET-compatible IDE

### Build

```bash
# Restore NuGet packages
dotnet restore StockPulse.sln

# Build all projects
dotnet build StockPulse.sln --configuration Release
```

### Integrating into a WPF Application

Register the entire backend stack with a single call in your composition root (`App.xaml.cs` or equivalent):

```csharp
using Microsoft.Extensions.DependencyInjection;
using StockPulse.BLL.Extensions;

var services = new ServiceCollection();
services.AddStockPulseBackend("stockpulse.db");
var provider = services.BuildServiceProvider();

// Idempotent — creates tables and indexes on first run, no-ops on subsequent launches
await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
```

Resolve services wherever your ViewModels need them:

```csharp
var authService    = provider.GetRequiredService<IAuthService>();
var productService = provider.GetRequiredService<IProductService>();
var txService      = provider.GetRequiredService<ITransactionService>();
```

---

## Design Decisions

| Concern | Decision | Rationale |
|---|---|---|
| Data access | Dapper over EF Core | Explicit SQL, predictable query plans, no hidden N+1 |
| Concurrency | WAL mode + full `async/await` | Readers don't block writers; UI thread is never blocked |
| Transactions | Caller-managed connection passed into repositories | Stock update and audit log are guaranteed to share one transaction |
| Error handling | `Result<T>` union type | Forces callers to handle failures explicitly; no silent swallowing |
| RBAC placement | Service layer, not repository | Repositories are infrastructure — policy belongs in the domain |
| Schema versioning | `CREATE TABLE IF NOT EXISTS` | Lightweight idempotent guard sufficient for an embedded desktop DB |

---

## License

This project is licensed under the terms of the [LICENSE](LICENSE) file included in this repository.
