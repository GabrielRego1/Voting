using System.Diagnostics;
using System.Globalization;
using Workload.Abstractions.Metrics;
using Workload.Reporting;

namespace Workload.Metrics;

/// <summary>
/// Periodically writes the load progress to <c>error</c> (stderr), keeping stdout reserved for the report.
/// </summary>
internal sealed class ProgressReporter(TimeSpan interval) : IProgressReporter
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Writes one line per interval until cancelled. Cancellation ends the loop without throwing.
    /// </summary>
    public async Task RunAsync(TextWriter error, ReportBuilder report, TimeSpan duration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(report);
        var stopwatch = Stopwatch.StartNew();
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await error.WriteLineAsync(Format(stopwatch.Elapsed, duration, report.Snapshot()));
            }
        }
        catch (OperationCanceledException)
        {
            // Load finished or the user interrupted the run.
        }
    }

    public static string Format(TimeSpan elapsed, TimeSpan duration, ProgressSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{elapsed.TotalSeconds,4:0}s/{duration.TotalSeconds:0}s] sent {snapshot.Sent} | accepted {snapshot.Accepted} | rejected {snapshot.Rejected} | transport errors {snapshot.TransportErrors} | in flight {snapshot.InFlight}");
    }
}
