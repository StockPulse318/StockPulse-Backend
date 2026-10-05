using StockPulse.BLL.Interfaces;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<Result<IEnumerable<Category>>> GetAllAsync()
    {
        try
        {
            var categories = await _categoryRepository.GetAllAsync();
            return Result<IEnumerable<Category>>.Success(categories);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Category>>.Failure("Failed to retrieve categories.", "INTERNAL_ERROR", ex);
        }
    }

    public async Task<Result<Category>> GetByIdAsync(int id)
    {
        try
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            return category is not null
                ? Result<Category>.Success(category)
                : Result<Category>.Failure($"Category with ID {id} not found.", "NOT_FOUND");
        }
        catch (Exception ex)
        {
            return Result<Category>.Failure("Failed to retrieve category.", "INTERNAL_ERROR", ex);
        }
    }

    public async Task<Result<Category>> AddAsync(string actorRole, string name)
    {
        if (actorRole != UserRoles.WarehouseManager)
            return Result<Category>.Failure("Only warehouse managers can create categories.", "FORBIDDEN");

        if (string.IsNullOrWhiteSpace(name))
            return Result<Category>.Failure("Category name cannot be empty.", "VALIDATION_ERROR");

        try
        {
            var trimmed = name.Trim();
            var existing = await _categoryRepository.GetByNameAsync(trimmed);
            if (existing is not null)
                return Result<Category>.Failure($"Category '{trimmed}' already exists.", "CONFLICT");

            var newId = await _categoryRepository.AddAsync(new Category { Name = trimmed });
            return Result<Category>.Success(new Category { Id = newId, Name = trimmed });
        }
        catch (Exception ex)
        {
            return Result<Category>.Failure("Failed to create category.", "INTERNAL_ERROR", ex);
        }
    }
}
