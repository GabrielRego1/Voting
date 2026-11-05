namespace Workload.ApiClient;

/// <summary>
/// Final HTTP outcome of a vote after automatic retries; <see cref="StatusCode"/> is null on transport errors.
/// </summary>
internal sealed record VoteResponse(int? StatusCode, string? ProblemCode)
{
    public static VoteResponse TransportError { get; } = new(null, null);

    public bool IsConfirmation => StatusCode is 200 or 201;
}
