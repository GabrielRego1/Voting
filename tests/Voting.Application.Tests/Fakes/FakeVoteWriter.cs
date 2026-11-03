using System.Collections.Concurrent;
using Voting.Application.Abstractions;
using Voting.Application.Votes;
using Voting.Domain.Votes;

namespace Voting.Application.Tests.Fakes;

/// <summary>
/// Records written votes and answers with a configurable outcome (default: accepted).
/// </summary>
public sealed class FakeVoteWriter : IVoteWriter
{
    private readonly ConcurrentQueue<Vote> _writes = new();

    public Func<Vote, VoteWriteOutcome> Respond { get; set; } = vote => VoteWriteOutcome.Accepted(VoteReceipt.From(vote));

    public IReadOnlyCollection<Vote> Writes => _writes;

    public List<string>? Events { get; set; }

    public List<DateTimeOffset> Flushes { get; } = [];

    public Task FlushUntilAsync(DateTimeOffset instant, CancellationToken cancellationToken = default)
    {
        Flushes.Add(instant);
        Events?.Add("flush");
        return Task.CompletedTask;
    }

    public Task<VoteWriteOutcome> WriteAsync(Vote vote, CancellationToken cancellationToken = default)
    {
        _writes.Enqueue(vote);
        return Task.FromResult(Respond(vote));
    }
}
