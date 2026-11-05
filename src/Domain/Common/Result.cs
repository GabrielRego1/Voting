namespace Domain.Common;

/// <summary>
/// Outcome of an operation that either succeeds or fails with a typed <see cref="DomainError"/>.
/// </summary>
public class Result
{
    protected Result(DomainError? error)
    {
        Error = error;
    }

    public DomainError? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result Success()
    {
        return new Result(null);
    }

    public static Result Failure(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static Result<T> Success<T>(T value)
    {
        return new Result<T>(value, null);
    }

    public static Result<T> Failure<T>(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }
}
