using Domain.Results;
using Infrastructure.Persistence;
using Infrastructure.Tests.Fixtures;

namespace Infrastructure.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class ResultRepositoryTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TryAddAsync_FirstResult_PersistsAndMarksPollFinalized()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        var result = ResultCalculator.Calculate(
            poll,
            new Dictionary<Guid, long> { [poll.Participants[0].Id] = 400, [poll.Participants[1].Id] = 400, [poll.Participants[2].Id] = 200 },
            poll.ClosesAt!.Value.AddSeconds(1));
        var sut = new ResultRepository(dataSource);

        var added = await sut.TryAddAsync(result, Ct);
        var loaded = await sut.GetAsync(poll.Id, Ct);
        var reloadedPoll = await new PollRepository(dataSource).GetAsync(poll.Id, Ct);

        Assert.True(added);
        Assert.NotNull(loaded);
        Assert.Equal(result.PollId, loaded.PollId);
        Assert.Equal(result.TotalVotes, loaded.TotalVotes);
        Assert.Equal(result.Outcome, loaded.Outcome);
        Assert.Equal(result.EliminatedParticipantId, loaded.EliminatedParticipantId);
        Assert.Equal(result.TiedParticipantIds, loaded.TiedParticipantIds);
        Assert.Equal(result.Tallies, loaded.Tallies);
        Assert.All(loaded.Tallies, t => Assert.Equal(2, t.Percentage.Scale));
        Assert.Equal(result.FinalizedAt, loaded.FinalizedAt);
        Assert.True(reloadedPoll!.IsFinalized);
    }

    [Fact]
    public async Task TryAddAsync_SecondResult_ChangesNothing()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        var sut = new ResultRepository(dataSource);
        var first = ResultCalculator.Calculate(
            poll,
            new Dictionary<Guid, long> { [poll.Participants[0].Id] = 1 },
            poll.ClosesAt!.Value.AddSeconds(1));
        var second = ResultCalculator.Calculate(
            poll,
            new Dictionary<Guid, long> { [poll.Participants[1].Id] = 9 },
            poll.ClosesAt.Value.AddSeconds(2));
        await sut.TryAddAsync(first, Ct);

        var added = await sut.TryAddAsync(second, Ct);
        var loaded = await sut.GetAsync(poll.Id, Ct);

        Assert.False(added);
        Assert.Equal(first.TotalVotes, loaded!.TotalVotes);
        Assert.Equal(first.EliminatedParticipantId, loaded.EliminatedParticipantId);
        Assert.Equal(first.FinalizedAt, loaded.FinalizedAt);
    }

    [Fact]
    public async Task GetAsync_NoResult_ReturnsNull()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);

        Assert.Null(await new ResultRepository(dataSource).GetAsync(poll.Id, Ct));
    }
}
