using Workload.Reporting;

namespace Workload.Abstractions.Metrics;

/// <summary>
/// Periodically writes the load progress while a run is sending votes.
/// </summary>
internal interface IProgressReporter
{
    /// <summary>
    /// Writes one line per interval to <paramref name="error"/> until cancelled. Cancellation ends it without throwing.
    /// </summary>
    Task RunAsync(TextWriter error, ReportBuilder report, TimeSpan duration, CancellationToken cancellationToken);
}
