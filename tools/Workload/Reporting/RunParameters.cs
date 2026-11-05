namespace Workload.Reporting;

internal sealed record RunParameters(int Rate, int DurationSeconds, IReadOnlyList<int> Distribution, double RetryFraction, int Connections);
