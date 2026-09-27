# StockPulse — Backend

Enterprise-grade Data Access Layer (DAL) and Business Logic Layer (BLL) for a desktop Warehouse Inventory Management System built on **.NET 8**, **SQLite**, **Dapper**, and **Clean Architecture**.

---

## Project Structure

```
StockPulse-Backend/
├── StockPulse.sln
├── StockPulse.Domain/          # Pure domain — no infrastructure dependencies
│   ├── Entities/
│   │   ├── User.cs             # Auth entity + UserRoles constants
│   │   ├── Product.cs          # Inventory entity with computed IsLowStock
│   │   └── InventoryTransactionLog.cs  # Immutable ledger record + TransactionTypes
│   ├── Exceptions/
│   │   ├── InsufficientStockException.cs
│   │   ├── DuplicateProductException.cs
│   │   └── UnauthorizedActionException.cs
│   └── Results/
│       └── Result.cs           # Generic Result<T> and non-generic Result
│
├── StockPulse.DAL/             # Database I/O — Dapper + Microsoft.Data.Sqlite
│   ├── DatabaseInitializer.cs  # Schema creation, WAL mode, pragma enforcement
│   ├── Interfaces/
│   │   ├── IUserRepository.cs
│   │   ├── IProductRepository.cs
│   │   └── ITransactionLogRepository.cs
│   └── Repositories/
│       ├── UserRepository.cs
│       ├── ProductRepository.cs
│       └── TransactionLogRepository.cs
│
└── StockPulse.BLL/             # Business rules, RBAC, transactional boundaries
    ├── Extensions/
    │   └── ServiceCollectionExtensions.cs  # DI registration entry point
    ├── Interfaces/
    │   ├── IAuthService.cs
    │   ├── IProductService.cs
    │   └── ITransactionService.cs
    └── Services/
        ├── AuthService.cs       # PBKDF2-SHA256 hashing, login, user management
        ├── ProductService.cs    # CRUD with RBAC guards and field validation
        └── TransactionService.cs # Atomic Stock-In / Stock-Out with audit logging
```

---

## Key Design Decisions

| Concern | Approach |
|---|---|
| **ORM** | Dapper — thin, explicit SQL, no hidden N+1 queries |
| **Concurrency** | SQLite WAL mode + `async/await` throughout; no UI thread blocking |
| **Password storage** | PBKDF2-HMAC-SHA256, 310,000 iterations, 128-bit random salt per password |
| **Atomicity** | Stock movements open one connection, one explicit transaction covering both the `UPDATE Products` and the `INSERT INTO InventoryTransactionLogs` |
| **RBAC** | Enforced at the service layer before any DB call; typed `UnauthorizedActionException` surfaced as a `Result.Failure` |
| **Error handling** | `Result<T>` / `Result` discriminated union — no raw exception propagation to the UI |
| **SQL injection** | 100% parameterized queries via Dapper; LIKE wildcards escaped server-side |
| **Low stock detection** | `Product.IsLowStock` computed property — zero extra queries, always consistent with the fetched snapshot |

---

## Wiring into a WPF Application

In your WPF `App.xaml.cs` (or a dedicated composition root):

```csharp
using Microsoft.Extensions.DependencyInjection;
using StockPulse.BLL.Extensions;

var services = new ServiceCollection();
services.AddStockPulseBackend("stockpulse.db");
var provider = services.BuildServiceProvider();

// Run once at startup — idempotent, safe to call on every launch
await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
```

Then resolve services wherever needed:

```csharp
var authService   = provider.GetRequiredService<IAuthService>();
var productService = provider.GetRequiredService<IProductService>();
var txService     = provider.GetRequiredService<ITransactionService>();
```

---

## Terminal Quickstart (for building without Rider)

> These commands use the .NET SDK bundled with Rider on Linux.

```bash
# Alias the bundled dotnet so you don't have to type the full path each time
DOTNET=~/.local/share/JetBrains/Toolbox/apps/rider/lib/ReSharperHost/linux-x64/dotnet/dotnet

# Restore NuGet packages
$DOTNET restore StockPulse.sln

# Build the entire solution
$DOTNET build StockPulse.sln --configuration Release

# Build a specific project only
$DOTNET build StockPulse.DAL/StockPulse.DAL.csproj
```

---

## Branch & Merge Workflow

| Branch | Purpose |
|---|---|
| `main` | Stable, production-ready code only |
| `feature/dal-bll-implementation` | This branch — full DAL + BLL implementation |

Push and open a PR when ready:
```bash
git push origin feature/dal-bll-implementation
```
Then open a Pull Request on GitHub/GitLab from `feature/dal-bll-implementation` → `main`.
