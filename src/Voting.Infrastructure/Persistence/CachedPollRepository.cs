using System.Collections.Concurrent;
using Voting.Application.Abstractions;
using Voting.Domain.Polls;

namespace Voting.Infrastructure.Persistence;

/// <summary>
/// Caches opened polls so voting does not read the database on every vote.
/// An opened poll is immutable while its window is open and once finalized; between closing and
/// finalization the snapshot is re-read so the <c>Finalized</c> status is observed. Polls that were
/// not opened yet are never cached, because opening mutates them.
/// </summary>
public sealed class CachedPollRepository(IPollRepository inner, TimeProvider time) : IPollRepository
{
    private readonly ConcurrentDictionary<Guid, Poll> _snapshots = new();

    public Task AddAsync(Poll poll, CancellationToken cancellationToken = default)
    {
        return inner.AddAsync(poll, cancellationToken);
    }

    public async Task<Poll?> GetAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        if (_snapshots.TryGetValue(pollId, out var cached) && IsReusable(cached, now))
        {
            return cached;
        }

        var poll = await inner.GetAsync(pollId, cancellationToken);
        if (poll is not null && IsReusable(poll, now))
        {
            _snapshots[pollId] = poll;
        }
        else
        {
            _snapshots.TryRemove(pollId, out _);
        }

        return poll;
    }

    public async Task<bool> TryOpenAsync(Poll poll, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(poll);
        try
        {
            return await inner.TryOpenAsync(poll, cancellationToken);
        }
        finally
        {
            _snapshots.TryRemove(poll.Id, out _);
        }
    }

    public Task<IReadOnlyList<Poll>> ListDueForFinalizationAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return inner.ListDueForFinalizationAsync(now, cancellationToken);
    }

    private static bool IsReusable(Poll poll, DateTimeOffset now)
    {
        return poll.IsFinalized || (poll.ClosesAt is { } closesAt && now <= closesAt);
    }
}
