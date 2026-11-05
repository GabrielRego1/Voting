using System.Text.Json;
using Workload.Metrics;

namespace Workload.ApiClient;

/// <summary>
/// Client of the public voting API (contracts/voting-api.openapi.yaml).
/// </summary>
internal interface IVotingApiClient : IDisposable
{
    HttpClient Http { get; }

    Task<PollInfo> CreatePollAsync(IReadOnlyList<string> participants, CancellationToken cancellationToken);

    Task<PollInfo> OpenPollAsync(Guid pollId, CancellationToken cancellationToken);

    Task<PollInfo> GetPollAsync(Guid pollId, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a vote, retrying with the same key on <c>503</c> or transport errors. Every attempt's latency is recorded.
    /// </summary>
    Task<VoteResponse> CastVoteAsync(
        Guid pollId,
        Guid participantId,
        string idempotencyKey,
        LatencyRecorder latency,
        Action onRetry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the poll result, or <see langword="null"/> while it is not available yet.
    /// </summary>
    Task<JsonElement?> GetResultAsync(Guid pollId, CancellationToken cancellationToken);
}
