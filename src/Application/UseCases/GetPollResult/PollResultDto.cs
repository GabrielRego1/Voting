using Application.Dtos;
using Domain.Entities;
using Domain.Enums;

namespace Application.UseCases.GetPollResult;

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
