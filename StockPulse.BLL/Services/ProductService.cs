using StockPulse.BLL.Interfaces;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Exceptions;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Services;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IUserRepository _userRepository;

    public ProductService(IProductRepository productRepository, IUserRepository userRepository)
    {
        _productRepository = productRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<Product>> GetByIdAsync(int productId)
    {
        try
        {
            var product = await _productRepository.GetByIdAsync(productId);
            return product is not null
                ? Result<Product>.Success(product)
                : Result<Product>.Failure($"No product found with ID {productId}.");
        }
        catch (Exception ex)
        {
            return Result<Product>.Failure("Failed to retrieve product.", ex);
        }
    }

    public async Task<Result<IEnumerable<Product>>> GetAllAsync()
    {
        try
        {
            var products = await _productRepository.GetAllAsync();
            return Result<IEnumerable<Product>>.Success(products);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Product>>.Failure("Failed to retrieve products.", ex);
        }
    }

    public async Task<Result<IEnumerable<Product>>> SearchByNameAsync(string partialName)
    {
        if (string.IsNullOrWhiteSpace(partialName))
            return Result<IEnumerable<Product>>.Failure("Search term cannot be empty.");

        try
        {
            var products = await _productRepository.SearchByNameAsync(partialName.Trim());
            return Result<IEnumerable<Product>>.Success(products);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Product>>.Failure("Search failed.", ex);
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
            return Result<IEnumerable<Product>>.Failure("Failed to retrieve low-stock products.", ex);
        }
    }

    public async Task<Result<int>> AddProductAsync(string actorUsername, Product product)
    {
        try
        {
            var actor = await ResolveActorAsync(actorUsername);
            EnforceManagerRole(actor, "add products");

            var validationError = ValidateProductFields(product);
            if (validationError is not null)
                return Result<int>.Failure(validationError);

            var duplicate = await _productRepository.GetByNameAsync(product.ProductName);
            if (duplicate is not null)
                throw new DuplicateProductException(product.ProductName);

            var newId = await _productRepository.AddAsync(product);
            return Result<int>.Success(newId);
        }
        catch (UnauthorizedActionException ex)
        {
            return Result<int>.Failure(ex.Message, ex);
        }
        catch (DuplicateProductException ex)
        {
            return Result<int>.Failure(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure("Failed to add product.", ex);
        }
    }

    public async Task<Result> UpdateProductAsync(string actorUsername, Product product)
    {
        try
        {
            var actor = await ResolveActorAsync(actorUsername);
            EnforceManagerRole(actor, "update products");

            var validationError = ValidateProductFields(product);
            if (validationError is not null)
                return Result.Failure(validationError);

            var existing = await _productRepository.GetByIdAsync(product.ProductID);
            if (existing is null)
                return Result.Failure($"Product with ID {product.ProductID} does not exist.");

            // Check for name conflict only when the name is actually changing.
            if (!existing.ProductName.Equals(product.ProductName, StringComparison.OrdinalIgnoreCase))
            {
                var nameConflict = await _productRepository.GetByNameAsync(product.ProductName);
                if (nameConflict is not null)
                    throw new DuplicateProductException(product.ProductName);
            }

            await _productRepository.UpdateAsync(product);
            return Result.Success();
        }
        catch (UnauthorizedActionException ex)
        {
            return Result.Failure(ex.Message, ex);
        }
        catch (DuplicateProductException ex)
        {
            return Result.Failure(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Result.Failure("Failed to update product.", ex);
        }
    }

    public async Task<Result> DeleteProductAsync(string actorUsername, int productId)
    {
        try
        {
            var actor = await ResolveActorAsync(actorUsername);
            EnforceManagerRole(actor, "delete products");

            var existing = await _productRepository.GetByIdAsync(productId);
            if (existing is null)
                return Result.Failure($"Product with ID {productId} does not exist.");

            await _productRepository.DeleteAsync(productId);
            return Result.Success();
        }
        catch (UnauthorizedActionException ex)
        {
            return Result.Failure(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Result.Failure("Failed to delete product.", ex);
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<User> ResolveActorAsync(string actorUsername)
    {
        return await _userRepository.GetByUsernameAsync(actorUsername)
            ?? throw new InvalidOperationException($"Actor '{actorUsername}' not found in the system.");
    }

    private static void EnforceManagerRole(User actor, string attemptedAction)
    {
        if (!actor.IsWarehouseManager)
            throw new UnauthorizedActionException(actor.Username, actor.Role, attemptedAction);
    }

    /// <summary>
    /// Returns a human-readable error string on first validation failure, or null if all fields are valid.
    /// Centralised here so both Add and Update paths run the same checks without duplication.
    /// </summary>
    private static string? ValidateProductFields(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.ProductName))
            return "Product name is required.";

        if (string.IsNullOrWhiteSpace(product.Category))
            return "Category is required.";

        if (product.UnitPrice <= 0)
            return "Unit price must be greater than zero.";

        if (product.ReorderLevel < 0)
            return "Reorder level must be zero or greater.";

        return null;
    }
}
