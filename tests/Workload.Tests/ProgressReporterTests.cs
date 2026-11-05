using Workload.Metrics;
using Workload.Reporting;

namespace Workload.Tests;

public sealed class ProgressReporterTests
{
    [Fact]
    public async Task RunAsync_UntilCancelled_WritesProgressLines()
    {
        var builder = new ReportBuilder();
        builder.RecordSent();
        using var error = new StringWriter();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = new ProgressReporter(TimeSpan.FromMilliseconds(20))
            .RunAsync(error, builder, TimeSpan.FromSeconds(3), cancellation.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await run;

        Assert.Contains("sent 1 |", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("in flight 1", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_Snapshot_ShowsElapsedOverDurationAndCounters()
    {
        var line = ProgressReporter.Format(
            TimeSpan.FromSeconds(10.4),
            TimeSpan.FromSeconds(35),
            new ProgressSnapshot(100000, 99870, 0, 0, 130));

        Assert.Equal("[  10s/35s] sent 100000 | accepted 99870 | rejected 0 | transport errors 0 | in flight 130", line);
    }
}
