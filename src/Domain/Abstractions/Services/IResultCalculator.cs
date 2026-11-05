using Domain.Entities;

namespace Domain.Abstractions.Services;

/// <summary>
/// Tallies a poll into its final, immutable result.
/// </summary>
public interface IResultCalculator
{
    PollResult Calculate(Poll poll, IReadOnlyDictionary<Guid, long> votesByParticipant, DateTimeOffset finalizedAt);
}
