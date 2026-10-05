using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StockPulse.API.DTOs;
using StockPulse.API.Seeding;
using StockPulse.DAL;

namespace StockPulse.Tests;

public class StockPulseTestFixture : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    public StockPulseTestFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"stockpulse_test_{Guid.NewGuid():N}.db");
    }

    public string DbPath => _dbPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DatabaseInitializer));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            var db = new DatabaseInitializer(_dbPath);
            services.AddSingleton(db);
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        await DatabaseSeeder.SeedAsync(db, config);
    }

    public async Task<HttpClient> CreateManagerClientAsync()
    {
        var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest("manager", "StockPulse@2026"));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    public async Task<HttpClient> CreateClerkClientAsync()
    {
        var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest("clerk", "StockPulse@2026"));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch { }
    }
}
