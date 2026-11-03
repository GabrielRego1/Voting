namespace Voting.Application.Abstractions;

public interface IVoteTally
{
    /// <summary>
    /// Counts committed votes per participant, including participants with zero votes.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, long>> CountByParticipantAsync(Guid pollId, CancellationToken cancellationToken = default);
}
