using Voting.Infrastructure.Persistence;
using Voting.Infrastructure.Tests.Fixtures;

namespace Voting.Infrastructure.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class VoteTallyTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CountByParticipantAsync_VotesForTwoParticipants_GroupsCountsIncludingZero()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        await VoteSeeder.SeedVotesAsync(dataSource, poll, Ct, 7, 0, 3);

        var counts = await new VoteTally(dataSource).CountByParticipantAsync(poll.Id, Ct);

        Assert.Equal(3, counts.Count);
        Assert.Equal(7, counts[poll.Participants[0].Id]);
        Assert.Equal(0, counts[poll.Participants[1].Id]);
        Assert.Equal(3, counts[poll.Participants[2].Id]);
    }
}
