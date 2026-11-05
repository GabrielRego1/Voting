using System.Collections.Concurrent;
using Application.Abstractions;
using Domain.Results;

namespace Application.Tests.Fakes;

/// <summary>
/// Stores each poll result once and marks the poll as finalized, like the real repository.
/// </summary>
public sealed class InMemoryResultRepository(InMemoryPollRepository polls) : IResultRepository
{
    private readonly ConcurrentDictionary<Guid, PollResult> _results = new();

    public Task<bool> TryAddAsync(PollResult result, CancellationToken cancellationToken = default)
    {
        if (!_results.TryAdd(result.PollId, result))
        {
            return Task.FromResult(false);
        }

        polls.MarkFinalized(result.PollId);
        return Task.FromResult(true);
    }

    public Task<PollResult?> GetAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_results.GetValueOrDefault(pollId));
    }
}
