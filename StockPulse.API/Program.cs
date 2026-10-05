using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StockPulse.API.DTOs;
using StockPulse.API.Middleware;
using StockPulse.API.Seeding;
using StockPulse.API.Services;
using StockPulse.BLL.Extensions;
using StockPulse.DAL;

var builder = WebApplication.CreateBuilder(args);

// Read database path from DATABASE_PATH environment variable (or fallback to appsettings / default)
var dbPath = Environment.GetEnvironmentVariable("DATABASE_PATH")
    ?? builder.Configuration["DatabasePath"]
    ?? "stockpulse.db";

builder.Services.AddStockPulseBackend(dbPath);

// JWT Authentication configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? jwtSettings["Secret"]
    ?? "stockpulse-default-super-secure-jwt-secret-key-2026-min-32-chars";

var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? jwtSettings["Issuer"]
    ?? "StockPulse";

var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? jwtSettings["Audience"]
    ?? "StockPulseClient";

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
            ValidIssuer              = issuer,
            ValidAudience            = audience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ClockSkew                = TimeSpan.Zero
        };

        // Consistent error responses for unauthorized / forbidden requests
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ErrorResponse(
                    new ErrorDetail("UNAUTHORIZED", "Authentication is required to access this resource.")));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ErrorResponse(
                    new ErrorDetail("FORBIDDEN", "You do not have permission to access this resource.")));
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<TokenService>();

// Rate Limiting on Login Endpoint (Fixed window)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ErrorResponse(
            new ErrorDetail("RATE_LIMIT_EXCEEDED", "Too many login attempts. Please try again later.")), token);
    };
    options.AddFixedWindowLimiter("login-policy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

// Configure Controllers and standard error format for invalid models
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var firstError = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(msg => !string.IsNullOrWhiteSpace(msg))
                ?? "The submitted request failed validation.";

            return new BadRequestObjectResult(new ErrorResponse(
                new ErrorDetail("VALIDATION_ERROR", firstError)));
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "StockPulse API",
        Version     = "v1",
        Description = "Warehouse Inventory Management System — REST API"
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Reference    = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, [] } });
});

var app = builder.Build();

// CLI Command Handling: "seed" and "reset"
if (args.Length > 0)
{
    if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await DatabaseSeeder.SeedAsync(db, app.Configuration);
        return;
    }

    if (args.Contains("reset", StringComparer.OrdinalIgnoreCase))
    {
        var env = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? app.Environment.EnvironmentName;
        var isProduction = string.Equals(env, "production", StringComparison.OrdinalIgnoreCase);
        var hasForce = args.Contains("--force", StringComparer.OrdinalIgnoreCase);

        if (isProduction && !hasForce)
        {
            Console.Error.WriteLine("[RESET] Error: Refusing to reset schema in production environment without explicit --force flag.");
            Environment.ExitCode = 1;
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await db.ResetAsync();
        Console.WriteLine("[RESET] Database schema cleared and recreated through versioned migrations.");
        return;
    }
}

// Ensure database schema migrations are applied at startup (never seeds automatically)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await db.MigrateAsync();
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "StockPulse API v1");
    options.RoutePrefix = "docs";
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }

