using Domain.Enums;
using Domain.ValueObjects;

namespace Domain.Entities;

/// <summary>
/// The final, immutable result of a poll, written once at finalization.
/// </summary>
public sealed record PollResult(
    Guid PollId,
    long TotalVotes,
    IReadOnlyList<ParticipantTally> Tallies,
    Outcome Outcome,
    Guid? EliminatedParticipantId,
    IReadOnlyList<Guid> TiedParticipantIds,
    DateTimeOffset FinalizedAt);
