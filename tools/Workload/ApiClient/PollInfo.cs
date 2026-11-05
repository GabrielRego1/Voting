namespace Workload.ApiClient;

internal sealed record PollInfo(Guid Id, IReadOnlyList<Guid> ParticipantIds, DateTimeOffset? ClosesAt);
