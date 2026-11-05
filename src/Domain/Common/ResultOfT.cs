namespace Domain.Common;

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
