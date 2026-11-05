namespace Workload.Metrics;

/// <summary>
/// Counters shown while the load runs.
/// </summary>
internal sealed record ProgressSnapshot(long Sent, long Accepted, long Rejected, long TransportErrors, long InFlight);
