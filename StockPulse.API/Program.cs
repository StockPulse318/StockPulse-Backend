using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StockPulse.API.Middleware;
using StockPulse.API.Seeding;
using StockPulse.API.Services;
using StockPulse.BLL.Extensions;
using StockPulse.DAL;

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

// In Development: value comes from appsettings.Development.json (gitignored).
// In Production:  value comes from the platform environment variable
//                 JwtSettings__Secret (double-underscore = nested key in ASP.NET Core).
var secret = jwtSettings["Secret"]
    ?? throw new InvalidOperationException(
        "JwtSettings:Secret is not configured. " +
        "Add it to appsettings.Development.json locally, " +
        "or set the JwtSettings__Secret environment variable in production.");

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
// Schema initialisation + seeding — both idempotent, safe on every startup
// ---------------------------------------------------------------------------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await db.InitializeAsync();
    await DatabaseSeeder.SeedAsync(db, app.Configuration);
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
