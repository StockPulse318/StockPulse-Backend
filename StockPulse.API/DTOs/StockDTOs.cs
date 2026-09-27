namespace StockPulse.API.DTOs;

public sealed record StockMovementRequest(int Quantity);

public sealed record TransactionLogResponse(
    int TransactionID,
    int ProductID,
    string TransactionType,
    int QuantityChanged,
    string HandledBy,
    string Timestamp);
