using System.Globalization;
using System.Text.Json;

namespace Workload;

internal static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void Write(Report report, OutputFormat format, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(writer);
        if (format == OutputFormat.Json)
        {
            writer.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
            return;
        }

        var c = report.Counts;
        var p = report.Parameters;
        var l = report.LatencyMs;
        var lines = new List<(string Field, string Value)>
        {
            ("pollId", report.PollId.ToString()),
            ("parameters", string.Create(CultureInfo.InvariantCulture, $"rate={p.Rate} durationSeconds={p.DurationSeconds} distribution={string.Join(',', p.Distribution)} retryFraction={p.RetryFraction} connections={p.Connections}")),
            ("sent", Format(c.Sent)),
            ("accepted", Format(c.Accepted)),
            ("replayed", Format(c.Replayed)),
            ("retried", Format(c.Retried)),
        };
        lines.AddRange(c.Rejected.Select(kv => ($"rejected.{kv.Key}", Format(kv.Value))));
        lines.Add(("transportErrors", Format(c.TransportErrors)));
        lines.Add(("achievedRate", Format(report.AchievedRate)));
        lines.Add(("latencyMs", string.Create(CultureInfo.InvariantCulture, $"p50={l.P50:0.0} p95={l.P95:0.0} p99={l.P99:0.0} max={l.Max:0.0}")));
        lines.Add(("result", report.Result is { } result ? result.GetRawText() : "(not available)"));
        lines.Add(("checks.acceptedEqualsTotalVotes", Format(report.Checks.AcceptedEqualsTotalVotes)));
        lines.Add(("checks.noDuplicatesFromReplays", Format(report.Checks.NoDuplicatesFromReplays)));
        lines.Add(("checks.p95WithinTarget", Format(report.Checks.P95WithinTarget)));

        var width = lines.Max(line => line.Field.Length);
        foreach (var (field, value) in lines)
        {
            writer.WriteLine($"{field.PadRight(width)}  {value}");
        }
    }

    private static string Format(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Format(double value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string Format(bool value)
    {
        return value ? "true" : "false";
    }
}
