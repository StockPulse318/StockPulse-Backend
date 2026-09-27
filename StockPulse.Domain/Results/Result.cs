namespace StockPulse.Domain.Results;

/// <summary>
/// Discriminated union for operation outcomes. Forces every service caller to
/// explicitly handle both success and failure paths — no silent swallowing of
/// exceptions via empty catch blocks, and no null-return guessing games.
///
/// Usage pattern:
///   var result = await productService.AddProductAsync(...);
///   if (result.IsSuccess) { use result.Value } else { show result.ErrorMessage }
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T? Value { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    private Result(string errorMessage, Exception? exception = null)
    {
        IsSuccess = false;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string errorMessage, Exception? exception = null)
        => new(errorMessage, exception);

    /// <summary>
    /// Allows transforming a Result{T} into a Result{TOut} without unwrapping.
    /// Useful for chaining service calls without nested if-blocks.
    /// </summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
        IsSuccess && Value is not null
            ? Result<TOut>.Success(mapper(Value))
            : Result<TOut>.Failure(ErrorMessage!, Exception);
}

/// <summary>
/// Non-generic Result for operations that have no meaningful return value
/// (e.g., Delete, Update). Avoids the awkward Result{bool} anti-pattern.
/// </summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private Result(bool success, string? errorMessage = null, Exception? exception = null)
    {
        IsSuccess = success;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static Result Success() => new(true);

    public static Result Failure(string errorMessage, Exception? exception = null)
        => new(false, errorMessage, exception);
}
