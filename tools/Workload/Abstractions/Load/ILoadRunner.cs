using Workload.Abstractions.ApiClient;
using Workload.ApiClient;
using Workload.Configuration;
using Workload.Metrics;
using Workload.Reporting;

namespace Workload.Abstractions.Load;

/// <summary>
/// Sends the open-model vote load of a run.
/// </summary>
internal interface ILoadRunner
{
    /// <summary>
    /// Sends the load and waits for every request to finish. Returns the time spent sending.
    /// </summary>
    Task<TimeSpan> RunAsync(
        IVotingApiClient client,
        PollInfo poll,
        WorkloadOptions options,
        ReportBuilder report,
        LatencyRecorder latency,
        CancellationToken cancellationToken);
}
