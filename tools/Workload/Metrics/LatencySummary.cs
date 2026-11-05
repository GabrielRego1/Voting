namespace Workload.Metrics;

internal sealed record LatencySummary(double P50, double P95, double P99, double Max);
