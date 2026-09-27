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

## Running Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Setup

```bash
git clone <repo-url>
cd StockPulse-Backend

dotnet restore StockPulse.sln
```

Set a JWT secret in `StockPulse.API/appsettings.json`:

```json
"JwtSettings": {
  "Secret": "your-secret-at-least-32-characters-long"
}
```

Run:

```bash
dotnet run --project StockPulse.API/StockPulse.API.csproj
```

The database file is created automatically on first run. No migrations needed.

---

## Configuration

All configuration lives in `StockPulse.API/appsettings.json`. Environment variables override any key using the standard ASP.NET Core double-underscore convention:

| Key | Purpose |
|-----|---------|
| `DatabasePath` | Path to the SQLite file |
| `JwtSettings__Secret` | Signing key — keep this out of source control |
| `JwtSettings__Issuer` | Token issuer string |
| `JwtSettings__Audience` | Token audience string |
| `JwtSettings__ExpiryHours` | Token lifetime in hours (default: 8) |
| `AllowedOrigins__0` | First allowed CORS origin |

---

## License

See [LICENSE](LICENSE).
