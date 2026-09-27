using StockPulse.BLL.Interfaces;
using StockPulse.DAL;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Exceptions;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Services;

public sealed class TransactionService : ITransactionService
{
    private readonly IProductRepository _productRepository;
    private readonly ITransactionLogRepository _logRepository;
    private readonly IUserRepository _userRepository;
    private readonly DatabaseInitializer _db;

    public TransactionService(
        IProductRepository productRepository,
        ITransactionLogRepository logRepository,
        IUserRepository userRepository,
        DatabaseInitializer db)
    {
        _productRepository = productRepository;
        _logRepository     = logRepository;
        _userRepository    = userRepository;
        _db                = db;
    }

    public async Task<Result> StockInAsync(string actorUsername, int productId, int quantity)
    {
        if (quantity <= 0)
            return Result.Failure("Stock-In quantity must be a positive integer.");

        try
        {
            await ResolveActorAsync(actorUsername);

            var product = await _productRepository.GetByIdAsync(productId);
            if (product is null)
                return Result.Failure($"Product with ID {productId} does not exist.");

            await ExecuteStockMovementAsync(actorUsername, productId, delta: quantity, TransactionTypes.StockIn);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("Stock-In operation failed.", ex);
        }
    }

    public async Task<Result> StockOutAsync(string actorUsername, int productId, int quantity)
    {
        if (quantity <= 0)
            return Result.Failure("Stock-Out quantity must be a positive integer.");

        try
        {
            await ResolveActorAsync(actorUsername);

            var product = await _productRepository.GetByIdAsync(productId);
            if (product is null)
                return Result.Failure($"Product with ID {productId} does not exist.");

            if (product.Quantity < quantity)
                throw new InsufficientStockException(productId, product.Quantity, quantity);

            await ExecuteStockMovementAsync(actorUsername, productId, delta: -quantity, TransactionTypes.StockOut);
            return Result.Success();
        }
        catch (InsufficientStockException ex)
        {
            return Result.Failure(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Result.Failure("Stock-Out operation failed.", ex);
        }
    }

    public async Task<Result<IEnumerable<InventoryTransactionLog>>> GetAllLogsAsync(string actorUsername)
    {
        try
        {
            await ResolveActorAsync(actorUsername);
            var logs = await _logRepository.GetAllAsync();
            return Result<IEnumerable<InventoryTransactionLog>>.Success(logs);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<InventoryTransactionLog>>.Failure("Failed to retrieve transaction logs.", ex);
        }
    }

    public async Task<Result<IEnumerable<InventoryTransactionLog>>> GetLogsByProductAsync(
        string actorUsername, int productId)
    {
        try
        {
            await ResolveActorAsync(actorUsername);
            var logs = await _logRepository.GetByProductIdAsync(productId);
            return Result<IEnumerable<InventoryTransactionLog>>.Success(logs);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<InventoryTransactionLog>>.Failure(
                $"Failed to retrieve logs for product {productId}.", ex);
        }
    }

    public async Task<Result<IEnumerable<InventoryTransactionLog>>> GetLogsByUserAsync(
        string actorUsername, string targetUsername)
    {
        try
        {
            var actor = await ResolveActorAsync(actorUsername);

            // Stock Clerks can only query their own logs.
            if (actor.IsStockClerk &&
                !actor.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedActionException(
                    actor.Username, actor.Role, $"view transaction logs for '{targetUsername}'");
            }

            var logs = await _logRepository.GetByUserAsync(targetUsername);
            return Result<IEnumerable<InventoryTransactionLog>>.Success(logs);
        }
        catch (UnauthorizedActionException ex)
        {
            return Result<IEnumerable<InventoryTransactionLog>>.Failure(ex.Message, ex);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<InventoryTransactionLog>>.Failure("Failed to retrieve user logs.", ex);
        }
    }

    // Opens one connection and transaction shared across both the quantity update and the log insert.
    // If either statement fails, the entire transaction is rolled back — no orphaned stock changes,
    // no log entries without a matching quantity movement.
    private async Task ExecuteStockMovementAsync(
        string actorUsername,
        int productId,
        int delta,
        string transactionType)
    {
        await using var connection  = await _db.CreateConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await _productRepository.AdjustQuantityAsync(productId, delta, connection,
                (Microsoft.Data.Sqlite.SqliteTransaction)transaction);

            var log = new InventoryTransactionLog
            {
                ProductID       = productId,
                TransactionType = transactionType,
                QuantityChanged = Math.Abs(delta),
                HandledBy       = actorUsername
            };

            await _logRepository.AddAsync(log, connection,
                (Microsoft.Data.Sqlite.SqliteTransaction)transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<User> ResolveActorAsync(string actorUsername)
    {
        return await _userRepository.GetByUsernameAsync(actorUsername)
            ?? throw new InvalidOperationException($"Actor '{actorUsername}' not found.");
    }
}
