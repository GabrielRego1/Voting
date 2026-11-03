using Voting.Application.Votes;
using Voting.Domain.Votes;

namespace Voting.Application.Abstractions;

/// <summary>
/// Durably records votes. The returned task completes only after the vote is committed (FR-008).
/// </summary>
public interface IVoteWriter
{
    Task<VoteWriteOutcome> WriteAsync(Vote vote, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes once every vote received up to <paramref name="instant"/> that is already queued has been committed.
    /// </summary>
    Task FlushUntilAsync(DateTimeOffset instant, CancellationToken cancellationToken = default);
}
