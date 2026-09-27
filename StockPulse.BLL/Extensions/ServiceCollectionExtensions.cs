using Microsoft.Extensions.DependencyInjection;
using StockPulse.BLL.Interfaces;
using StockPulse.BLL.Services;
using StockPulse.DAL;
using StockPulse.DAL.Interfaces;
using StockPulse.DAL.Repositories;

namespace StockPulse.BLL.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStockPulseBackend(
        this IServiceCollection services,
        string databaseFilePath)
    {
        // Singleton — one instance owns the connection string and pragma config for the process lifetime.
        services.AddSingleton(new DatabaseInitializer(databaseFilePath));

        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<ITransactionLogRepository, TransactionLogRepository>();

        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<IProductService, ProductService>();
        services.AddTransient<ITransactionService, TransactionService>();

        return services;
    }
}
