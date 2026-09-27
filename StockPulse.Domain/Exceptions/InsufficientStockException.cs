namespace StockPulse.Domain.Exceptions;

/// <summary>
/// Thrown during a Stock-Out operation when the requested quantity exceeds
/// what is currently on hand. The transaction is rolled back before this is raised.
/// </summary>
public sealed class InsufficientStockException : Exception
{
    public int ProductID { get; }
    public int Available { get; }
    public int Requested { get; }

    public InsufficientStockException(int productId, int available, int requested)
        : base($"Stock-Out denied for ProductID {productId}: requested {requested}, but only {available} available.")
    {
        ProductID = productId;
        Available = available;
        Requested = requested;
    }
}
