namespace SharedKernel;

/// <summary>
/// Represents an error with a machine-readable code and a human-readable message.
/// </summary>
public sealed record Error(string Code, string Message);

/// <summary>
/// Discriminated union result type. Either holds a successful value or an Error.
/// Eliminates null returns and exception-driven control flow for expected failures.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed Result.");

    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result.");

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _error = error;
        IsSuccess = false;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
}
