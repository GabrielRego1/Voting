using System.Text.Json;

namespace Voting.Workload;

internal sealed record RunParameters(int Rate, int DurationSeconds, IReadOnlyList<int> Distribution, double RetryFraction, int Connections);

internal sealed record ReportCounts(
    long Sent,
    long Accepted,
    long Replayed,
    long Retried,
    IReadOnlyDictionary<string, long> Rejected,
    long TransportErrors);

internal sealed record ReportChecks(bool AcceptedEqualsTotalVotes, bool NoDuplicatesFromReplays, bool P95WithinTarget)
{
    public bool AllPassed => AcceptedEqualsTotalVotes && NoDuplicatesFromReplays && P95WithinTarget;
}

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
    public int ExitCode => Checks.AllPassed ? 0 : 1;
}
