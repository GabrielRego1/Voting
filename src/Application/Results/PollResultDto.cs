using Application.Polls;
using Domain.Polls;
using Domain.Results;

namespace Application.Results;

public sealed record ParticipantTallyDto(Guid ParticipantId, string Name, long Votes, decimal Percentage);

public sealed record PollResultDto(
    Guid PollId,
    long TotalVotes,
    Outcome Outcome,
    ParticipantDto? Eliminated,
    IReadOnlyList<ParticipantDto> TiedParticipants,
    IReadOnlyList<ParticipantTallyDto> Tallies,
    DateTimeOffset FinalizedAt)
{
    public static PollResultDto From(Poll poll, PollResult result)
    {
        ArgumentNullException.ThrowIfNull(poll);
        ArgumentNullException.ThrowIfNull(result);
        var participants = poll.Participants.ToDictionary(p => p.Id, p => new ParticipantDto(p.Id, p.Name, p.Position));
        return new PollResultDto(
            result.PollId,
            result.TotalVotes,
            result.Outcome,
            result.EliminatedParticipantId is { } eliminated ? participants[eliminated] : null,
            result.TiedParticipantIds.Select(id => participants[id]).ToArray(),
            result.Tallies.Select(t => new ParticipantTallyDto(t.ParticipantId, t.Name, t.Votes, t.Percentage)).ToArray(),
            result.FinalizedAt);
    }
}
