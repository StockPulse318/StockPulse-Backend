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
        string? databaseFilePath = null)
    {
        services.AddSingleton(new DatabaseInitializer(databaseFilePath));

        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<ICategoryRepository, CategoryRepository>();
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<IStockMovementRepository, StockMovementRepository>();

        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<ICategoryService, CategoryService>();
        services.AddTransient<IProductService, ProductService>();

        return services;
    }
}
