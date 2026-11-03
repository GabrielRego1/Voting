using Voting.Application.Abstractions;

namespace Voting.Application.Tests.Fakes;

public sealed class FakeVoteTally : IVoteTally
{
    public Dictionary<Guid, long> Counts { get; } = [];

    public List<string>? Events { get; set; }

    public Task<IReadOnlyDictionary<Guid, long>> CountByParticipantAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        Events?.Add("count");
        return Task.FromResult<IReadOnlyDictionary<Guid, long>>(new Dictionary<Guid, long>(Counts));
    }
}
