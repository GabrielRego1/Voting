using Domain.Results;

namespace Application.Abstractions;

public interface IResultRepository
{
    /// <summary>
    /// Stores the result and marks the poll as finalized, only if no result exists yet.
    /// Returns <see langword="false"/> when a result was already stored.
    /// </summary>
    Task<bool> TryAddAsync(PollResult result, CancellationToken cancellationToken = default);

    Task<PollResult?> GetAsync(Guid pollId, CancellationToken cancellationToken = default);
}
