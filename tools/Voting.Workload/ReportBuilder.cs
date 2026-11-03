using System.Collections.Concurrent;
using System.Text.Json;

namespace Voting.Workload;

/// <summary>
/// Final HTTP outcome of a vote after automatic retries; <see cref="StatusCode"/> is null on transport errors.
/// </summary>
internal sealed record VoteResponse(int? StatusCode, string? ProblemCode)
{
    public static VoteResponse TransportError { get; } = new(null, null);

    public bool IsConfirmation => StatusCode is 200 or 201;
}

/// <summary>
/// Thread-safe counters of a run. <c>accepted</c> counts distinct confirmed keys; intentional resends count in
/// <c>replayed</c>; rejections are grouped by problem <c>code</c>.
/// </summary>
internal sealed class ReportBuilder
{
    public const double P95TargetMs = 200;

    private readonly ConcurrentDictionary<string, byte> _confirmedKeys = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _rejected = new(StringComparer.Ordinal);
    private long _sent;
    private long _accepted;
    private long _replayed;
    private long _replaysCreated;
    private long _retried;
    private long _transportErrors;

    public void RecordSent()
    {
        Interlocked.Increment(ref _sent);
    }

    public void RecordRetry()
    {
        Interlocked.Increment(ref _retried);
    }

    public void RecordResponse(string key, VoteResponse response, bool intentionalReplay)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.StatusCode is null)
        {
            Interlocked.Increment(ref _transportErrors);
            return;
        }

        if (!response.IsConfirmation)
        {
            _rejected.AddOrUpdate(response.ProblemCode ?? response.StatusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), 1, (_, n) => n + 1);
            return;
        }

        if (intentionalReplay)
        {
            Interlocked.Increment(ref _replayed);
            if (response.StatusCode == 201)
            {
                // A resend with an already confirmed key must never create a new vote (SC-004).
                Interlocked.Increment(ref _replaysCreated);
            }

            return;
        }

        if (_confirmedKeys.TryAdd(key, 0))
        {
            Interlocked.Increment(ref _accepted);
        }
        else
        {
            Interlocked.Increment(ref _replayed);
        }
    }

    public Report Build(Guid pollId, RunParameters parameters, TimeSpan sendingTime, LatencySummary latency, JsonElement? result)
    {
        var sent = Interlocked.Read(ref _sent);
        var accepted = Interlocked.Read(ref _accepted);
        var counts = new ReportCounts(
            sent,
            accepted,
            Interlocked.Read(ref _replayed),
            Interlocked.Read(ref _retried),
            new SortedDictionary<string, long>(_rejected, StringComparer.Ordinal),
            Interlocked.Read(ref _transportErrors));

        var totalVotes = result is { } r && r.TryGetProperty("totalVotes", out var total) ? total.GetInt64() : (long?)null;
        var checks = new ReportChecks(
            totalVotes == accepted,
            Interlocked.Read(ref _replaysCreated) == 0,
            latency.P95 <= P95TargetMs);
        var achievedRate = sendingTime > TimeSpan.Zero ? Math.Round(sent / sendingTime.TotalSeconds, 1) : 0;

        return new Report(pollId, parameters, counts, achievedRate, latency, result, checks);
    }
}
