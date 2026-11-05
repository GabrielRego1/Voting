using System.Text.Json;
using System.Text.Json.Serialization;
using Workload.Metrics;

namespace Workload.Reporting;

/// <summary>
/// Run report with the fields of contracts/workload-cli.md.
/// </summary>
internal sealed record Report(
    Guid PollId,
    RunParameters Parameters,
    ReportCounts Counts,
    double AchievedRate,
    LatencySummary LatencyMs,
    JsonElement? Result,
    ReportChecks Checks)
{
    /// <summary>0 when every check passed, 1 otherwise.</summary>
    [JsonIgnore]
    public int ExitCode => Checks.AllPassed ? 0 : 1;
}
