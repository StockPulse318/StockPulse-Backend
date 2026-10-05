namespace StockPulse.Domain.Results;

// Forces callers to explicitly handle success and failure — no null returns, no bare exceptions.
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    private Result(string errorMessage, string errorCode = "ERROR", Exception? exception = null)
    {
        IsSuccess = false;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string errorMessage, string errorCode = "ERROR", Exception? exception = null)
        => new(errorMessage, errorCode, exception);

    public static Result<T> Failure(string errorMessage, Exception? exception)
        => new(errorMessage, "ERROR", exception);

    // Transforms the inner value without unwrapping — useful for mapping service results to DTOs.
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
        IsSuccess && Value is not null
            ? Result<TOut>.Success(mapper(Value))
            : Result<TOut>.Failure(ErrorMessage!, ErrorCode ?? "ERROR", Exception);
}

// Non-generic variant for operations with no return value (Delete, Update, etc.).
public sealed class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private Result(bool success, string? errorMessage = null, string? errorCode = null, Exception? exception = null)
    {
        IsSuccess = success;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static Result Success() => new(true);

    public static Result Failure(string errorMessage, string errorCode = "ERROR", Exception? exception = null)
        => new(false, errorMessage, errorCode, exception);

    public static Result Failure(string errorMessage, Exception? exception)
        => new(false, errorMessage, "ERROR", exception);
}
