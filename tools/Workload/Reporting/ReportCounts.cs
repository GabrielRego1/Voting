namespace Workload.Reporting;

internal sealed record ReportCounts(
    long Sent,
    long Accepted,
    long Replayed,
    long Retried,
    IReadOnlyDictionary<string, long> Rejected,
    long TransportErrors);
