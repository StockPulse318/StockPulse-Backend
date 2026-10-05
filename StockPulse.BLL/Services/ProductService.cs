using StockPulse.BLL.Interfaces;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Services;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;

    public ProductService(IProductRepository productRepository, ICategoryRepository categoryRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<Result<Product>> GetByIdAsync(int productId)
    {
        try
        {
            var product = await _productRepository.GetByIdAsync(productId);
            return product is not null
                ? Result<Product>.Success(product)
                : Result<Product>.Failure($"No product found with ID {productId}.", "NOT_FOUND");
        }
        catch (Exception ex)
        {
            return Result<Product>.Failure("Failed to retrieve product.", "INTERNAL_ERROR", ex);
        }
    }

    public async Task<Result<(IEnumerable<Product> Items, int TotalCount)>> GetPagedAsync(
        int page,
        int limit,
        string? sortBy = null,
        string? sortOrder = null,
        int? categoryId = null,
        string? q = null)
    {
        try
        {
            var result = await _productRepository.GetPagedAsync(page, limit, sortBy, sortOrder, categoryId, q);
            return Result<(IEnumerable<Product> Items, int TotalCount)>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<(IEnumerable<Product> Items, int TotalCount)>.Failure("Failed to retrieve products.", "INTERNAL_ERROR", ex);
        }
    }

    public async Task<Result<IEnumerable<Product>>> GetLowStockAsync()
    {
        try
        {
            var products = await _productRepository.GetLowStockAsync();
            return Result<IEnumerable<Product>>.Success(products);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Product>>.Failure("Failed to retrieve low-stock products.", "INTERNAL_ERROR", ex);
        }
    }

    public async Task<Result<Product>> AddProductAsync(string actorRole, Product product)
    {
        if (actorRole != UserRoles.WarehouseManager)
            return Result<Product>.Failure("Only warehouse managers can add products.", "FORBIDDEN");

        var validationError = ValidateProduct(product);
        if (validationError != null)
            return Result<Product>.Failure(validationError, "VALIDATION_ERROR");

        try
        {
            var category = await _categoryRepository.GetByIdAsync(product.CategoryId);
            if (category is null)
                return Result<Product>.Failure($"Category with ID {product.CategoryId} does not exist.", "VALIDATION_ERROR");

            var duplicateCode = await _productRepository.GetByCodeAsync(product.ProductCode);
            if (duplicateCode is not null)
                return Result<Product>.Failure($"Product code '{product.ProductCode}' is already in use.", "DUPLICATE_PRODUCT_CODE");

            var newId = await _productRepository.AddAsync(product);
            var created = await _productRepository.GetByIdAsync(newId);
            return Result<Product>.Success(created!);
        }
        catch (Exception ex)
        {
            return Result<Product>.Failure("Failed to add product.", "INTERNAL_ERROR", ex);
        }
    }

    public async Task<Result<Product>> UpdateProductAsync(string actorRole, Product product)
    {
        if (actorRole != UserRoles.WarehouseManager)
            return Result<Product>.Failure("Only warehouse managers can modify products.", "FORBIDDEN");

        var validationError = ValidateProduct(product);
        if (validationError != null)
            return Result<Product>.Failure(validationError, "VALIDATION_ERROR");

        try
        {
            var existing = await _productRepository.GetByIdAsync(product.Id);
            if (existing is null)
                return Result<Product>.Failure($"Product with ID {product.Id} does not exist.", "NOT_FOUND");

            var category = await _categoryRepository.GetByIdAsync(product.CategoryId);
            if (category is null)
                return Result<Product>.Failure($"Category with ID {product.CategoryId} does not exist.", "VALIDATION_ERROR");

            if (!existing.ProductCode.Equals(product.ProductCode, StringComparison.OrdinalIgnoreCase))
            {
                var duplicateCode = await _productRepository.GetByCodeAsync(product.ProductCode);
                if (duplicateCode is not null && duplicateCode.Id != product.Id)
                    return Result<Product>.Failure($"Product code '{product.ProductCode}' is already in use.", "DUPLICATE_PRODUCT_CODE");
            }

            await _productRepository.UpdateAsync(product);
            var updated = await _productRepository.GetByIdAsync(product.Id);
            return Result<Product>.Success(updated!);
        }
        catch (Exception ex)
        {
            return Result<Product>.Failure("Failed to update product.", "INTERNAL_ERROR", ex);
        }
    }

    public async Task<Result> DeleteProductAsync(string actorRole, int productId)
    {
        if (actorRole != UserRoles.WarehouseManager)
            return Result.Failure("Only warehouse managers can delete products.", "FORBIDDEN");

        try
        {
            var existing = await _productRepository.GetByIdAsync(productId);
            if (existing is null)
                return Result.Failure($"Product with ID {productId} does not exist.", "NOT_FOUND");

            await _productRepository.DeleteAsync(productId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("Failed to delete product.", "INTERNAL_ERROR", ex);
        }
    }

    public async Task<Result> StockInAsync(int productId, int amount, int performedByUserId)
    {
        if (amount <= 0)
            return Result.Failure("Stock-In amount must be a positive integer.", "VALIDATION_ERROR");

        try
        {
            var (success, errorCode, errorMessage) = await _productRepository.ExecuteStockInAsync(productId, amount, performedByUserId);
            if (!success)
                return Result.Failure(errorMessage ?? "Stock-In operation failed.", errorCode ?? "ERROR");

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("Stock-In operation failed.", "INTERNAL_ERROR", ex);
        }
    }

    public async Task<Result> StockOutAsync(int productId, int amount, int performedByUserId)
    {
        if (amount <= 0)
            return Result.Failure("Stock-Out amount must be a positive integer.", "VALIDATION_ERROR");

        try
        {
            var (success, errorCode, errorMessage) = await _productRepository.ExecuteStockOutAsync(productId, amount, performedByUserId);
            if (!success)
                return Result.Failure(errorMessage ?? "Stock-Out operation failed.", errorCode ?? "ERROR");

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("Stock-Out operation failed.", "INTERNAL_ERROR", ex);
        }
    }

    private static string? ValidateProduct(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.ProductCode))
            return "Product code is required.";

        if (string.IsNullOrWhiteSpace(product.Name))
            return "Product name is required.";

        if (product.CategoryId <= 0)
            return "A valid category ID is required.";

        if (product.Quantity < 0)
            return "Quantity must be zero or greater.";

        if (product.UnitPrice < 0)
            return "Unit price must be zero or greater.";

        if (product.ReorderLevel < 0)
            return "Reorder level must be zero or greater.";

        return null;
    }
}
