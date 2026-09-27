using StockPulse.Domain.Entities;
using StockPulse.Domain.Results;

namespace StockPulse.BLL.Interfaces;

public interface ITransactionService
{
    Task<Result> StockInAsync(string actorUsername, int productId, int quantity);
    Task<Result> StockOutAsync(string actorUsername, int productId, int quantity);

    Task<Result<IEnumerable<InventoryTransactionLog>>> GetAllLogsAsync(string actorUsername);
    Task<Result<IEnumerable<InventoryTransactionLog>>> GetLogsByProductAsync(string actorUsername, int productId);
    Task<Result<IEnumerable<InventoryTransactionLog>>> GetLogsByUserAsync(string actorUsername, string targetUsername);
}
