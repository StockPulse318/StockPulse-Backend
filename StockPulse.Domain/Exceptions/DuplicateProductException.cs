namespace StockPulse.Domain.Exceptions;

/// <summary>
/// Raised when an insert or rename attempt would violate the UNIQUE constraint
/// on Products.ProductName. Caught at the service layer to return a clean Result
/// rather than leaking a raw SQLite exception to the caller.
/// </summary>
public sealed class DuplicateProductException : Exception
{
    public string ProductName { get; }

    public DuplicateProductException(string productName)
        : base($"A product named '{productName}' already exists.")
    {
        ProductName = productName;
    }
}
