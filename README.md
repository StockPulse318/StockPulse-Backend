# StockPulse Backend

REST API for a Warehouse Inventory Management System. Handles authentication, product inventory, stock movements, and transaction audit logging.

Built with ASP.NET Core 8, SQLite, and Dapper.

---

## Stack

| | |
|---|---|
| Runtime | .NET 8 |
| Framework | ASP.NET Core Web API |
| Database | SQLite (embedded) |
| Data access | Dapper + Microsoft.Data.Sqlite |
| Auth | JWT — HMAC-SHA256 signed tokens |
| Password hashing | PBKDF2-HMAC-SHA256, 310,000 iterations |

---

## Project Structure

```
StockPulse-Backend/
├── StockPulse.API/
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── ProductsController.cs
│   │   ├── StockController.cs
│   │   └── LogsController.cs
│   ├── DTOs/
│   ├── Middleware/
│   │   └── ExceptionMiddleware.cs
│   ├── Services/
│   │   └── TokenService.cs
│   └── Program.cs
├── StockPulse.BLL/
│   ├── Services/
│   │   ├── AuthService.cs
│   │   ├── ProductService.cs
│   │   └── TransactionService.cs
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs
├── StockPulse.DAL/
│   ├── DatabaseInitializer.cs
│   └── Repositories/
└── StockPulse.Domain/
    ├── Entities/
    ├── Exceptions/
    └── Results/
```

---

## API Endpoints

All protected endpoints require a `Bearer` token in the `Authorization` header.

### Auth

| Method | Endpoint | Access |
|--------|----------|--------|
| `POST` | `/api/auth/login` | Public |
| `POST` | `/api/auth/users` | Warehouse Manager |
| `GET` | `/api/auth/users` | Warehouse Manager |
| `DELETE` | `/api/auth/users/{username}` | Warehouse Manager |

### Products

| Method | Endpoint | Access |
|--------|----------|--------|
| `GET` | `/api/products` | Authenticated |
| `GET` | `/api/products/{id}` | Authenticated |
| `GET` | `/api/products/search?name={partial}` | Authenticated |
| `GET` | `/api/products/low-stock` | Authenticated |
| `POST` | `/api/products` | Warehouse Manager |
| `PUT` | `/api/products/{id}` | Warehouse Manager |
| `DELETE` | `/api/products/{id}` | Warehouse Manager |

### Stock Movements

| Method | Endpoint | Access |
|--------|----------|--------|
| `POST` | `/api/products/{id}/stock/in` | Authenticated |
| `POST` | `/api/products/{id}/stock/out` | Authenticated |

### Transaction Logs

| Method | Endpoint | Access |
|--------|----------|--------|
| `GET` | `/api/logs` | Warehouse Manager |
| `GET` | `/api/logs/product/{productId}` | Authenticated |
| `GET` | `/api/logs/user/{username}` | Manager — or own logs |

---

## Roles

Two roles are issued at registration and embedded in the JWT:

- **Warehouse Manager** — full access across all resources
- **Stock Clerk** — read products, execute stock movements, view own transaction logs

Role violations return `403 Forbidden`.

---

## Database Schema

SQLite with foreign key enforcement and WAL journaling enabled on every connection.

**Users** — `Username` (PK), `PasswordHash`, `Role`

**Products** — `ProductID` (PK), `ProductName` (unique), `Category`, `Quantity` (≥ 0), `UnitPrice` (> 0), `ReorderLevel`

**InventoryTransactionLogs** — `TransactionID` (PK), `ProductID` (FK → cascade delete), `TransactionType` (`Stock-In` / `Stock-Out`), `QuantityChanged`, `HandledBy` (FK → Users), `Timestamp`

---

## API Documentation

Swagger UI is available at `/docs` when the API is running.

- Locally: `http://localhost:5000/docs`
- Production: [SWAGGER Docs](https://stockpulse-backend-production-4c30.up.railway.app/docs)

Click **Authorize**, paste your Bearer token from the login response, and you can call
every endpoint directly from the browser.

---

## Running Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Setup

```bash
git clone <repo-url>
cd StockPulse-Backend

dotnet restore StockPulse.sln
```

Copy the development config example and fill in your values:

```bash
cp StockPulse.API/appsettings.Development.json.example StockPulse.API/appsettings.Development.json
```

At minimum, set a `JwtSettings.Secret` (32+ characters) in `appsettings.Development.json`.
This file is gitignored — it never gets committed.

Run:

```bash
dotnet run --project StockPulse.API/StockPulse.API.csproj
```

The database and first manager account are created automatically on first run.

---

## Configuration

`appsettings.json` is committed and contains non-secret defaults.
Secrets go in `appsettings.Development.json` locally (gitignored — copy from `.example`).
In production, set them as platform environment variables using ASP.NET Core’s double-underscore convention for nested keys:

| Key | Where to set it |
|-----|------|
| `JwtSettings__Secret` | `appsettings.Development.json` locally / env var in production |
| `JwtSettings__Issuer` | `appsettings.json` (not a secret) |
| `JwtSettings__Audience` | `appsettings.json` (not a secret) |
| `JwtSettings__ExpiryHours` | `appsettings.json` (not a secret) |
| `DatabasePath` | `appsettings.json` / env var if using a mounted volume |
| `AllowedOrigins__0` | `appsettings.Development.json` locally / env var in production |
| `SeedManager__Username` | `appsettings.Development.json` locally / env var in production |
| `SeedManager__Password` | `appsettings.Development.json` locally / env var in production |

---

## License

See [LICENSE](LICENSE).
