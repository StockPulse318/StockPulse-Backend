using Microsoft.Data.Sqlite;
using StockPulse.Domain.Entities;

namespace StockPulse.DAL.Interfaces;

public interface IStockMovementRepository
{
    Task<int> AddAsync(StockMovement movement, SqliteConnection connection, SqliteTransaction transaction);
    Task<IEnumerable<StockMovement>> GetByProductIdAsync(int productId);
    Task<IEnumerable<StockMovement>> GetAllAsync();
}
