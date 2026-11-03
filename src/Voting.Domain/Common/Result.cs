namespace Voting.Domain.Common;

/// <summary>
/// A typed business error identified by a stable <see cref="Code"/>.
/// </summary>
public sealed record DomainError(string Code, string Message);

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

/// <summary>
/// Outcome of an operation that produces a <typeparamref name="T"/> on success.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, DomainError? error)
        : base(error)
    {
        _value = value;
    }

    /// <summary>
    /// The success value. Throws when the result is a failure.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result failed with '{Error!.Code}' and has no value.");
}
