# StockPulse Backend

REST API for the **Warehouse Inventory Management System**, built to strictly fulfill the Product Requirements Document (PRD). Provides a centralized platform for storing product information, monitoring stock levels, executing transactional stock movements, and generating low-stock alerts.

Built with **C#**, **ASP.NET Core 8**, **SQLite**, and **Dapper**.

---

## Additions to the PRD

The following are the only additions made beyond the baseline PRD, with their justifications:

1. **`categories` table**: Enforces strict database-level normalization and separation of product name and category, preventing concatenated or malformed category strings.
2. **`stock_movements` table**: Records each stock-in and stock-out operation with quantity change, timestamp, and responsible user ID for auditability and ledger consistency.
3. **Rate limiting on `/auth/login`**: Protects the authentication endpoint against credential stuffing and brute-force attacks via a 10 requests/minute fixed window.

---

## Role Assumptions

- **Warehouse Manager (`WAREHOUSE_MANAGER`)**: Full administrative control over inventory — viewing products, searching, adding new products, modifying existing products, deleting products, creating categories, executing stock-in and stock-out, and reviewing low-stock alerts.
- **Stock Clerk (`CLERK`)**: Dedicated to routine operational duties — searching products, viewing product details, recording stock-in transactions, recording stock-out transactions, and reviewing low-stock alerts.
- **Assumption recorded per PRD**: The PRD states that clerks "search and update inventory information". This implementation interprets that as stock-in, stock-out, and search, with product catalog record modifications (name, price, reorder level, category, deletions) reserved exclusively for the warehouse manager.

The following features are **explicitly out of scope** and omitted: multiple warehouses/branches, barcode scanning, cloud synchronisation, mobile-specific features, and advanced analytics/reporting.

---

## Stack & Architecture

- **Language / Runtime**: C# 12 / .NET 8 (with cross-SDK compatibility)
- **Framework**: ASP.NET Core 8 Web API
- **Database**: SQLite (embedded) with foreign keys (`PRAGMA foreign_keys = ON;`) and WAL journaling (`PRAGMA journal_mode = WAL;`) enforced on every connection
- **Data Access**: Dapper micro-ORM with versioned migrations
- **Authentication**: JWT access tokens (lifetime configurable via `TOKEN_LIFETIME_HOURS`, default 8 hours)
- **Password Hashing**: BCrypt (`BCrypt.Net-Next`)
- **API Documentation**: OpenAPI / Swagger UI at `/docs`

```
StockPulse-Backend/
├── StockPulse.Domain/      # Domain entities (User, Product, Category, StockMovement), Results, Exceptions
├── StockPulse.DAL/         # SQLite access, Dapper repositories, DatabaseInitializer, Migrations
├── StockPulse.BLL/         # Business logic, services (AuthService, ProductService, CategoryService)
├── StockPulse.API/         # ASP.NET Core controllers, DTOs, TokenService, ExceptionMiddleware, Seeder
└── StockPulse.Tests/       # Automated xUnit test suite (concurrency, RBAC, boundary checks, etc.)
```

---

## Environment Variables

All secrets and configuration come from environment variables (documented in [`.env.example`](.env.example)):

| Variable | Default | Purpose |
|---|---|---|
| `DATABASE_PATH` | `stockpulse.db` | File path to the SQLite database (set to persistent mount path in production) |
| `ENVIRONMENT` | `development` | Deployment environment (`development` or `production`) |
| `TOKEN_LIFETIME_HOURS` | `8` | Expiry duration in hours for signed JWT access tokens |
| `JWT_SECRET` | *(fallback dev key)* | Secret key for signing HMAC-SHA256 JWT tokens (min 32 chars) |
| `JWT_ISSUER` | `StockPulse` | JWT issuer claim |
| `JWT_AUDIENCE` | `StockPulseClient` | JWT audience claim |
| `SEED_MANAGER_USERNAME` | `manager` | Username for the initial seeded warehouse manager |
| `SEED_CLERK_USERNAME` | `clerk` | Username for the initial seeded stock clerk |
| `SEED_DEFAULT_PASSWORD` | `StockPulse@2026` | Initial password for seeded manager and clerk accounts |

---

## Setup & Running Locally

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 1. Clone & Restore
```bash
git clone <repo-url>
cd StockPulse-Backend
dotnet restore StockPulse.sln
```

### 2. Configure Environment
Copy `.env.example` or set environment variables:
```bash
cp .env.example .env
```

### 3. Database Migration & Schema Setup
Schema migrations execute automatically when the application starts or via explicit CLI commands.

- **To run migrations and seed realistic sample data (standalone command):**
  ```bash
  dotnet run --project StockPulse.API/StockPulse.API.csproj -- seed
  ```
- **To reset and recreate the schema from scratch:**
  ```bash
  dotnet run --project StockPulse.API/StockPulse.API.csproj -- reset
  ```
  *(Note: In production environments `ENVIRONMENT=production`, the reset command will refuse to execute unless `--force` is explicitly provided: `dotnet run --project StockPulse.API/StockPulse.API.csproj -- reset --force`)*

### 4. Start the Application
```bash
dotnet run --project StockPulse.API/StockPulse.API.csproj
```
The server will start listening. Swagger UI / OpenAPI documentation is hosted at:
`http://localhost:5000/docs` (or assigned port).

---

## Deployment Notes (Persistent Disk Requirement)

Because SQLite is an embedded file-based database, hosting it in containerized environments (such as Render, Railway, Fly.io, AWS ECS, or Docker) requires mounting a **persistent storage volume** (disk):

1. Mount a persistent disk volume to a path such as `/data`.
2. Set the environment variable:
   ```bash
   DATABASE_PATH=/data/stockpulse.db
   ```
3. Set `ENVIRONMENT=production` and a strong `JWT_SECRET` (at least 32 characters).
4. Run the seed command once during initial deployment or maintenance:
   ```bash
   dotnet StockPulse.API.dll seed
   ```
5. Ensure write permissions on the mounted directory so SQLite can create WAL and SHM journal files.

---

## Automated Test Suite

A comprehensive automated test suite in `StockPulse.Tests` verifies:
- **Role Permissions**: Manager vs Clerk restrictions (Clerk receives 403 Forbidden on product/category creation, updates, and deletes).
- **Duplicate Product Codes**: Returns 409 Conflict with code `DUPLICATE_PRODUCT_CODE` on duplicate insertions or renames.
- **Negative-Stock Prevention**: Atomic checks reject stock-outs exceeding available inventory with 409 `INSUFFICIENT_STOCK`.
- **Concurrent Stock-Out**: Parallel requests against the same inventory product correctly serialize in transactions without negative inventory.
- **Search Functionality**: Case-insensitive partial matching on both product name and product code for search-as-you-type.
- **Low-Stock Boundary**: Strict verification of the rule `Quantity <= ReorderLevel` at the boundary (`Quantity == ReorderLevel` is low-stock, `Quantity == ReorderLevel + 1` is not).
- **Idempotent Seeding**: Re-running the seeder produces zero duplicate rows and preserves existing relational keys.

To execute the test suite:
```bash
dotnet test StockPulse.sln --logger "console;verbosity=normal"
```

---

## API Endpoint Reference

All protected endpoints require the HTTP header:
`Authorization: Bearer <token>`

Errors are consistently returned in the standard format:
```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable explanation of error."
  }
}
```

---

### 1. Health Check
- **Endpoint**: `GET /health` (or `/api/health`)
- **Access**: Public (No authentication required)
- **Response**: `200 OK`
```json
{
  "status": "healthy",
  "timestamp": "2026-10-05T18:00:00.0000000Z"
}
```

---

### 2. User Login
- **Endpoint**: `POST /auth/login` (or `/api/auth/login`)
- **Access**: Public (Rate-limited: 10 requests/min)
- **Request Body**:
```json
{
  "username": "manager",
  "password": "StockPulse@2026"
}
```
- **Response**: `200 OK`
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "username": "manager",
  "role": "WAREHOUSE_MANAGER",
  "fullName": "Warehouse Operations Manager"
}
```
- **Error Response (Invalid Credentials)**: `401 Unauthorized`
```json
{
  "error": {
    "code": "INVALID_CREDENTIALS",
    "message": "Invalid username or password."
  }
}
```

---

### 3. List Products (Paged, Filtered, Sorted, Searched)
- **Endpoint**: `GET /products` (or `/api/products`)
- **Access**: Authenticated (`WAREHOUSE_MANAGER` or `CLERK`)
- **Query Parameters**:
  - `page` (optional integer, default: `1`)
  - `limit` (optional integer, default: `25`, max: `100`)
  - `sortBy` (optional string: `name`, `quantity`, or `category`, default: `name`)
  - `sortOrder` (optional string: `asc` or `desc`, default: `asc`)
  - `category_id` (optional integer filter)
  - `q` (optional string: searches product name OR product code, case-insensitive partial match)
- **Example Request**: `GET /products?page=1&limit=25&q=cement`
- **Response**: `200 OK`
```json
{
  "items": [
    {
      "id": 1,
      "product_code": "PRD-1001",
      "name": "Portland Cement 50kg Grade 42.5N",
      "category_id": 1,
      "category": "Building & Construction",
      "quantity": 180,
      "unit_price": 88.50,
      "reorder_level": 50,
      "is_low_stock": false,
      "created_at": "2026-10-05T18:00:00.0000000Z",
      "updated_at": "2026-10-05T18:00:00.0000000Z"
    }
  ],
  "total": 1,
  "page": 1,
  "limit": 25,
  "total_pages": 1
}
```

---

### 4. Get Product By ID
- **Endpoint**: `GET /products/{id}` (or `/api/products/{id}`)
- **Access**: Authenticated (`WAREHOUSE_MANAGER` or `CLERK`)
- **Example Request**: `GET /products/8`
- **Response**: `200 OK`
```json
{
  "id": 8,
  "product_code": "PRD-1008",
  "name": "Copper Cable 2.5mm Roll (100m)",
  "category_id": 2,
  "category": "Electrical & Power",
  "quantity": 8,
  "unit_price": 320.00,
  "reorder_level": 15,
  "is_low_stock": true,
  "created_at": "2026-10-05T18:00:00.0000000Z",
  "updated_at": "2026-10-05T18:00:00.0000000Z"
}
```

---

### 5. Create Product
- **Endpoint**: `POST /products` (or `/api/products`)
- **Access**: `WAREHOUSE_MANAGER` only
- **Request Body**:
```json
{
  "product_code": "PRD-2001",
  "name": "Industrial Ear Defenders SNR 32dB",
  "category_id": 6,
  "quantity": 50,
  "unit_price": 75.00,
  "reorder_level": 15
}
```
- **Response**: `201 Created`
```json
{
  "id": 51,
  "product_code": "PRD-2001",
  "name": "Industrial Ear Defenders SNR 32dB",
  "category_id": 6,
  "category": "Safety & Security Gear",
  "quantity": 50,
  "unit_price": 75.00,
  "reorder_level": 15,
  "is_low_stock": false,
  "created_at": "2026-10-05T18:15:00.0000000Z",
  "updated_at": "2026-10-05T18:15:00.0000000Z"
}
```
- **Error Response (Duplicate Code)**: `409 Conflict`
```json
{
  "error": {
    "code": "DUPLICATE_PRODUCT_CODE",
    "message": "Product code 'PRD-2001' is already in use."
  }
}
```

---

### 6. Update Product
- **Endpoint**: `PUT /products/{id}` (or `/api/products/{id}`)
- **Access**: `WAREHOUSE_MANAGER` only
- **Request Body**:
```json
{
  "product_code": "PRD-2001",
  "name": "Industrial Ear Defenders High Noise 34dB",
  "category_id": 6,
  "unit_price": 85.00,
  "reorder_level": 20
}
```
- **Response**: `200 OK`
```json
{
  "id": 51,
  "product_code": "PRD-2001",
  "name": "Industrial Ear Defenders High Noise 34dB",
  "category_id": 6,
  "category": "Safety & Security Gear",
  "quantity": 50,
  "unit_price": 85.00,
  "reorder_level": 20,
  "is_low_stock": false,
  "created_at": "2026-10-05T18:15:00.0000000Z",
  "updated_at": "2026-10-05T18:20:00.0000000Z"
}
```

---

### 7. Delete Product
- **Endpoint**: `DELETE /products/{id}` (or `/api/products/{id}`)
- **Access**: `WAREHOUSE_MANAGER` only
- **Response**: `204 No Content`

---

### 8. List Categories
- **Endpoint**: `GET /categories` (or `/api/categories`)
- **Access**: Authenticated (`WAREHOUSE_MANAGER` or `CLERK`)
- **Response**: `200 OK`
```json
[
  { "id": 1, "name": "Building & Construction" },
  { "id": 2, "name": "Electrical & Power" },
  { "id": 3, "name": "Hardware & Tools" },
  { "id": 4, "name": "Plumbing & Drainage" },
  { "id": 5, "name": "Paints & Protective Coatings" },
  { "id": 6, "name": "Safety & Security Gear" },
  { "id": 7, "name": "Warehouse & Material Handling" }
]
```

---

### 9. Create Category
- **Endpoint**: `POST /categories` (or `/api/categories`)
- **Access**: `WAREHOUSE_MANAGER` only
- **Request Body**:
```json
{
  "name": "HVAC & Ventilation"
}
```
- **Response**: `201 Created`
```json
{
  "id": 8,
  "name": "HVAC & Ventilation"
}
```

---

### 10. Stock-In Operation
- **Endpoint**: `POST /products/{id}/stock-in` (or `/api/products/{id}/stock-in`)
- **Access**: Authenticated (`WAREHOUSE_MANAGER` or `CLERK`)
- **Request Body**:
```json
{
  "amount": 25
}
```
- **Response**: `200 OK`
```json
{
  "message": "25 units added to product ID 8.",
  "product_id": 8,
  "amount": 25
}
```

---

### 11. Stock-Out Operation
- **Endpoint**: `POST /products/{id}/stock-out` (or `/api/products/{id}/stock-out`)
- **Access**: Authenticated (`WAREHOUSE_MANAGER` or `CLERK`)
- **Request Body**:
```json
{
  "amount": 5
}
```
- **Response**: `200 OK`
```json
{
  "message": "5 units removed from product ID 8.",
  "product_id": 8,
  "amount": 5
}
```
- **Error Response (Insufficient Stock)**: `409 Conflict`
```json
{
  "error": {
    "code": "INSUFFICIENT_STOCK",
    "message": "Insufficient stock for product ID 8 to complete stock-out of 100 units."
  }
}
```

---

### 12. Low-Stock Alerts
- **Endpoint**: `GET /alerts/low-stock` (or `/api/alerts/low-stock`)
- **Access**: Authenticated (`WAREHOUSE_MANAGER` or `CLERK`)
- **Response**: `200 OK`
```json
[
  {
    "id": 8,
    "product_code": "PRD-1008",
    "name": "Copper Cable 2.5mm Roll (100m)",
    "category_id": 2,
    "category": "Electrical & Power",
    "quantity": 8,
    "unit_price": 320.00,
    "reorder_level": 15,
    "is_low_stock": true,
    "created_at": "2026-10-05T18:00:00.0000000Z",
    "updated_at": "2026-10-05T18:00:00.0000000Z"
  },
  {
    "id": 23,
    "product_code": "PRD-1023",
    "name": "Solid Brass Gate Valve 2in Female Thread",
    "category_id": 4,
    "category": "Plumbing & Drainage",
    "quantity": 4,
    "unit_price": 95.00,
    "reorder_level": 10,
    "is_low_stock": true,
    "created_at": "2026-10-05T18:00:00.0000000Z",
    "updated_at": "2026-10-05T18:00:00.0000000Z"
  }
]
```
