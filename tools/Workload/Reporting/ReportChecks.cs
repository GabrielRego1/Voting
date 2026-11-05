using System.Text.Json.Serialization;

namespace Workload.Reporting;

internal sealed record ReportChecks(bool AcceptedEqualsTotalVotes, bool NoDuplicatesFromReplays, bool P95WithinTarget)
{
    [JsonIgnore]
    public bool AllPassed => AcceptedEqualsTotalVotes && NoDuplicatesFromReplays && P95WithinTarget;
}
