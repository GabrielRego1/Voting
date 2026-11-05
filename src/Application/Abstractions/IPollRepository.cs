using Domain.Entities;

namespace Application.Abstractions;

public interface IPollRepository
{
    Task AddAsync(Poll poll, CancellationToken cancellationToken = default);

    Task<Poll?> GetAsync(Guid pollId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the opening of <paramref name="poll"/>. Returns <see langword="false"/> when another
    /// poll is already open or has not been finalized yet (FR-019).
    /// </summary>
    Task<bool> TryOpenAsync(Poll poll, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists opened polls whose <c>ClosesAt</c> is before <paramref name="now"/> and that have no result yet.
    /// </summary>
    Task<IReadOnlyList<Poll>> ListDueForFinalizationAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
