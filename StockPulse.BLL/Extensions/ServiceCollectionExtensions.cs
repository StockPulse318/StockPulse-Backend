using Microsoft.Extensions.DependencyInjection;
using StockPulse.BLL.Interfaces;
using StockPulse.BLL.Services;
using StockPulse.DAL;
using StockPulse.DAL.Interfaces;
using StockPulse.DAL.Repositories;

namespace StockPulse.BLL.Extensions;

/// <summary>
/// Single registration point for the entire backend stack.
/// The WPF application calls AddStockPulseBackend() in its startup/composition root,
/// then resolves services via IServiceProvider — keeping all DI wiring out of UI code.
///
/// Usage in WPF App.xaml.cs (or equivalent composition root):
///   var services = new ServiceCollection();
///   services.AddStockPulseBackend("stockpulse.db");
///   var provider = services.BuildServiceProvider();
///   await provider.GetRequiredService{DatabaseInitializer}().InitializeAsync();
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStockPulseBackend(
        this IServiceCollection services,
        string databaseFilePath)
    {
        // DatabaseInitializer is a singleton — one instance manages the connection
        // string and pragma setup for the lifetime of the application process.
        services.AddSingleton(new DatabaseInitializer(databaseFilePath));

        // Repositories are transient — they open and close a connection per operation,
        // so there is no shared mutable state that would make them unsafe to reuse.
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<ITransactionLogRepository, TransactionLogRepository>();

        // Services are transient for the same reason — they delegate all I/O to
        // their injected repositories and carry no instance-level state themselves.
        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<IProductService, ProductService>();
        services.AddTransient<ITransactionService, TransactionService>();

        return services;
    }
}
