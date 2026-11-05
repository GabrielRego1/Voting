namespace Workload.Metrics;

/// <summary>
/// Thread-safe latency samples in milliseconds, summarized with nearest-rank percentiles.
/// </summary>
internal sealed class LatencyRecorder
{
    private readonly Lock _lock = new();
    private readonly List<double> _samples = [];

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _samples.Count;
            }
        }
    }

    public void Record(double milliseconds)
    {
        lock (_lock)
        {
            _samples.Add(milliseconds);
        }
    }

    public LatencySummary Summarize()
    {
        double[] sorted;
        lock (_lock)
        {
            sorted = [.. _samples];
        }

        if (sorted.Length == 0)
        {
            return new LatencySummary(0, 0, 0, 0);
        }

        Array.Sort(sorted);
        return new LatencySummary(Percentile(sorted, 0.50), Percentile(sorted, 0.95), Percentile(sorted, 0.99), sorted[^1]);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var rank = (int)Math.Ceiling(percentile * sorted.Length);
        return sorted[Math.Clamp(rank, 1, sorted.Length) - 1];
    }
}
