using System.Diagnostics;
using Workload.ApiClient;
using Workload.Configuration;
using Workload.Metrics;
using Workload.Reporting;

namespace Workload.Load;

/// <summary>
/// Open-model load: every 10 ms tick sends the votes due for the elapsed time (<c>rate/100</c> per tick),
/// independently of responses, so slow responses never slow the arrival rate down.
/// </summary>
internal sealed class LoadRunner : ILoadRunner
{
    public static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Sends the load and waits for every request to finish. Returns the time spent sending.
    /// </summary>
    public async Task<TimeSpan> RunAsync(
        IVotingApiClient client,
        PollInfo poll,
        WorkloadOptions options,
        ReportBuilder report,
        LatencyRecorder latency,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(poll);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(latency);
        var picker = new ParticipantPicker(options.Distribution, new Random());
        var duration = TimeSpan.FromSeconds(options.DurationSeconds);
        var total = (long)options.Rate * options.DurationSeconds;
        var inFlight = new List<Task>((int)Math.Min(total, int.MaxValue));
        long scheduled = 0;

        var stopwatch = Stopwatch.StartNew();
        using var timer = new PeriodicTimer(Tick);
        while (scheduled < total && await timer.WaitForNextTickAsync(cancellationToken))
        {
            var elapsed = stopwatch.Elapsed < duration ? stopwatch.Elapsed : duration;
            var due = Math.Min(total, (long)(options.Rate * elapsed.TotalSeconds));
            for (; scheduled < due; scheduled++)
            {
                inFlight.Add(SendVoteAsync(client, poll, poll.ParticipantIds[picker.Pick()], options, report, latency, cancellationToken));
            }
        }

        var sendingTime = stopwatch.Elapsed;
        await Task.WhenAll(inFlight);
        return sendingTime;
    }

    private static async Task SendVoteAsync(
        IVotingApiClient client,
        PollInfo poll,
        Guid participantId,
        WorkloadOptions options,
        ReportBuilder report,
        LatencyRecorder latency,
        CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString("N");
        report.RecordSent();
        var response = await client.CastVoteAsync(poll.Id, participantId, key, latency, report.RecordRetry, cancellationToken);
        report.RecordResponse(key, response, intentionalReplay: false);

#pragma warning disable CA5394 // Sampling which votes to resend is not security-sensitive.
        if (response.IsConfirmation && Random.Shared.NextDouble() < options.RetryFraction)
#pragma warning restore CA5394
        {
            var replay = await client.CastVoteAsync(poll.Id, participantId, key, latency, report.RecordRetry, cancellationToken);
            report.RecordResponse(key, replay, intentionalReplay: true);
        }
    }
}
