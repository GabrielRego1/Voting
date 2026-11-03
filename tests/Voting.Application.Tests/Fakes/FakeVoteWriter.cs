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

    public Task<VoteWriteOutcome> WriteAsync(Vote vote, CancellationToken cancellationToken = default)
    {
        _writes.Enqueue(vote);
        return Task.FromResult(Respond(vote));
    }
}
