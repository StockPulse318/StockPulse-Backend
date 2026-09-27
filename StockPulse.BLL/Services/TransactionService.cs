using StockPulse.BLL.Interfaces;
using StockPulse.DAL;
using StockPulse.DAL.Interfaces;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Exceptions;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Services;

/// <summary>
/// Owns the atomic stock movement lifecycle.
///
/// Both StockIn and StockOut follow the same transactional pattern:
///   1. Open a single SqliteConnection with pragmas applied.
///   2. Begin an explicit SQLite transaction.
///   3. Perform the quantity update and log insert as one atomic unit.
///   4. Commit on success — or roll back entirely on any failure.
///
/// This guarantees the invariant: a stock level change ALWAYS has a corresponding
/// audit log entry, and an audit entry NEVER exists without a matching stock change.
/// </summary>
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
        _logRepository = logRepository;
        _userRepository = userRepository;
        _db = db;
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

            await ExecuteStockMovementAsync(
                actorUsername,
                productId,
                delta: quantity,
                TransactionTypes.StockIn);

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

            // Read current stock BEFORE opening the transaction so we can return a
            // clean typed failure without an open transaction sitting idle.
            var product = await _productRepository.GetByIdAsync(productId);
            if (product is null)
                return Result.Failure($"Product with ID {productId} does not exist.");

            if (product.Quantity < quantity)
                throw new InsufficientStockException(productId, product.Quantity, quantity);

            // Negative delta drives the UPDATE SET Quantity = Quantity + @Delta path.
            await ExecuteStockMovementAsync(
                actorUsername,
                productId,
                delta: -quantity,
                TransactionTypes.StockOut);

            return Result.Success();
        }
        catch (InsufficientStockException ex)
        {
            // Surface as a structured Failure so the UI can render a specific
            // low-stock alert rather than a generic error message.
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
        string actorUsername,
        int productId)
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
        string actorUsername,
        string targetUsername)
    {
        try
        {
            var actor = await ResolveActorAsync(actorUsername);

            // Stock Clerks can only query their own logs.
            // Warehouse Managers can query any user's logs.
            if (actor.IsStockClerk &&
                !actor.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedActionException(
                    actor.Username, actor.Role, $"view transaction logs for user '{targetUsername}'");
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

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Core transactional kernel shared by both StockIn and StockOut.
    ///
    /// A single connection is opened and reused for both the UPDATE and the INSERT
    /// so they share the same SQLite transaction. If either statement fails:
    ///   - The catch block calls transaction.RollbackAsync(), reverting both operations.
    ///   - The exception is re-thrown so the calling public method can wrap it in a Result.
    ///
    /// Using 'await using' on the connection ensures the handle is released even if
    /// the rollback itself throws, preventing connection leaks under error conditions.
    /// </summary>
    private async Task ExecuteStockMovementAsync(
        string actorUsername,
        int productId,
        int delta,
        string transactionType)
    {
        await using var connection = await _db.CreateConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await _productRepository.AdjustQuantityAsync(productId, delta, connection,
                (Microsoft.Data.Sqlite.SqliteTransaction)transaction);

            var log = new InventoryTransactionLog
            {
                ProductID = productId,
                TransactionType = transactionType,
                QuantityChanged = Math.Abs(delta),
                HandledBy = actorUsername
            };

            await _logRepository.AddAsync(log, connection,
                (Microsoft.Data.Sqlite.SqliteTransaction)transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            // Roll back the entire unit — either both the quantity update and the
            // log insert succeed together, or neither is persisted to disk.
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<User> ResolveActorAsync(string actorUsername)
    {
        return await _userRepository.GetByUsernameAsync(actorUsername)
            ?? throw new InvalidOperationException($"Actor '{actorUsername}' not found in the system.");
    }
}
