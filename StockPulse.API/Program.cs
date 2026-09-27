using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StockPulse.API.Middleware;
using StockPulse.API.Seeding;
using StockPulse.API.Services;
using StockPulse.BLL.Extensions;
using StockPulse.DAL;

var builder = WebApplication.CreateBuilder(args);

var dbPath = builder.Configuration["DatabasePath"] ?? "stockpulse.db";
builder.Services.AddStockPulseBackend(dbPath);

var jwtSettings = builder.Configuration.GetSection("JwtSettings");

// Fails fast at startup if the secret is missing rather than serving 500s at runtime.
var secret = jwtSettings["Secret"]
    ?? throw new InvalidOperationException(
        "JwtSettings:Secret is not configured. Set it in appsettings.Development.json " +
        "locally or via the JwtSettings__Secret environment variable in production.");

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "StockPulse API",
        Version = "v1",
        Description = "Warehouse Inventory Management — REST API"
    });

    // Adds the Authorize button to Swagger UI so the frontend team can paste
    // a token and test protected endpoints directly in the browser.
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await db.InitializeAsync();
    await DatabaseSeeder.SeedAsync(db, app.Configuration);
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "StockPulse API v1");
    options.RoutePrefix = "docs";
});

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
