namespace Workload.Tests;

public sealed class LatencyRecorderTests
{
    [Fact]
    public void Summarize_OneToHundred_ReturnsNearestRankPercentiles()
    {
        var recorder = new LatencyRecorder();
        foreach (var sample in Enumerable.Range(1, 100).Reverse())
        {
            recorder.Record(sample);
        }

        var summary = recorder.Summarize();

        Assert.Equal(50, summary.P50);
        Assert.Equal(95, summary.P95);
        Assert.Equal(99, summary.P99);
        Assert.Equal(100, summary.Max);
    }

    [Fact]
    public void Summarize_SingleSample_AllPercentilesEqualSample()
    {
        var recorder = new LatencyRecorder();
        recorder.Record(4.2);

        var summary = recorder.Summarize();

        Assert.Equal(4.2, summary.P50);
        Assert.Equal(4.2, summary.P99);
        Assert.Equal(4.2, summary.Max);
    }

    [Fact]
    public void Summarize_NoSamples_ReturnsZeros()
    {
        var summary = new LatencyRecorder().Summarize();

        Assert.Equal(new LatencySummary(0, 0, 0, 0), summary);
    }

    [Fact]
    public async Task Record_ConcurrentWriters_KeepsEverySample()
    {
        var recorder = new LatencyRecorder();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                recorder.Record(1);
            }
        })));

        Assert.Equal(8000, recorder.Count);
    }
}
