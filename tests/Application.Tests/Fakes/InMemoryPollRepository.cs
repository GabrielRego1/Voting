using System.Collections.Concurrent;
using Application.Abstractions.Persistence;
using Domain.Entities;

namespace Application.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IPollRepository"/> that stores snapshots, so callers never share mutable instances.
/// </summary>
public sealed class InMemoryPollRepository : IPollRepository
{
    private readonly ConcurrentDictionary<Guid, Poll> _polls = new();
    private readonly Lock _openLock = new();

    public int Count => _polls.Count;

    public Task AddAsync(Poll poll, CancellationToken cancellationToken = default)
    {
        _polls[poll.Id] = Copy(poll, poll.IsFinalized);
        return Task.CompletedTask;
    }

    public Task<Poll?> GetAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_polls.TryGetValue(pollId, out var poll) ? Copy(poll, poll.IsFinalized) : null);
    }

    public Task<bool> TryOpenAsync(Poll poll, CancellationToken cancellationToken = default)
    {
        lock (_openLock)
        {
            if (_polls.Values.Any(p => p.Id != poll.Id && p.OpenedAt is not null && !p.IsFinalized))
            {
                return Task.FromResult(false);
            }

            _polls[poll.Id] = Copy(poll, poll.IsFinalized);
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<Poll>> ListDueForFinalizationAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Poll> due = _polls.Values
            .Where(p => p.ClosesAt < now && !p.IsFinalized)
            .Select(p => Copy(p, false))
            .ToArray();
        return Task.FromResult(due);
    }

    public void MarkFinalized(Guid pollId)
    {
        var poll = _polls[pollId];
        _polls[pollId] = Copy(poll, true);
    }

    private static Poll Copy(Poll poll, bool isFinalized)
    {
        return Poll.Rehydrate(poll.Id, poll.CreatedAt, poll.OpenedAt, poll.ClosesAt, isFinalized, poll.Participants);
    }
}
