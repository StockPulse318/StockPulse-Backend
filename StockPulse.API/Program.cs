using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StockPulse.API.Middleware;
using StockPulse.API.Services;
using StockPulse.BLL.Extensions;
using StockPulse.DAL;

// Load .env file variables into the process environment before the host builder
// reads configuration — this means they flow into IConfiguration automatically
// via the standard environment variable provider that ASP.NET Core includes by default.
DotNetEnv.Env.TraversePath().Load();

// Map the flat .env key names to the nested paths IConfiguration expects.
// This keeps appsettings.json free of any secrets while still letting the
// rest of the app read config through the standard IConfiguration abstraction.
Environment.SetEnvironmentVariable("JwtSettings__Secret",   Environment.GetEnvironmentVariable("JWT_SECRET"));
Environment.SetEnvironmentVariable("JwtSettings__Issuer",   Environment.GetEnvironmentVariable("JWT_ISSUER"));
Environment.SetEnvironmentVariable("JwtSettings__Audience", Environment.GetEnvironmentVariable("JWT_AUDIENCE"));
Environment.SetEnvironmentVariable("JwtSettings__ExpiryHours", Environment.GetEnvironmentVariable("JWT_EXPIRY_HOURS"));
Environment.SetEnvironmentVariable("DatabasePath",          Environment.GetEnvironmentVariable("DATABASE_PATH"));

// ALLOWED_ORIGINS is a comma-separated string in .env; split and map to indexed
// keys so ASP.NET Core's array binding picks them up correctly.
var rawOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? string.Empty;
var origins = rawOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries);
for (int i = 0; i < origins.Length; i++)
    Environment.SetEnvironmentVariable($"AllowedOrigins__{i}", origins[i].Trim());

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------

var dbPath = builder.Configuration["DatabasePath"] ?? "stockpulse.db";
builder.Services.AddStockPulseBackend(dbPath);

// ---------------------------------------------------------------------------
// JWT Authentication
// ---------------------------------------------------------------------------

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"]
    ?? throw new InvalidOperationException("JWT_SECRET is not set. Add it to your .env file.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSettings["Issuer"],
            ValidAudience            = jwtSettings["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<TokenService>();

// ---------------------------------------------------------------------------
// CORS
// ---------------------------------------------------------------------------

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("AllowedOrigins")
            .Get<string[]>() ?? [];

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Schema initialisation — idempotent, runs on every startup
// ---------------------------------------------------------------------------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await db.InitializeAsync();
    await StockPulse.API.Seeding.DatabaseSeeder.SeedAsync(db);
}

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
