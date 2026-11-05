using Domain.Entities;
using Infrastructure.Persistence.Migrations;
using Infrastructure.Persistence.Repositories;
using IntegrationTests.Infrastructure.Fixtures;
using Npgsql;

namespace IntegrationTests.Infrastructure.Persistence.Repositories;

[Collection(PostgresCollection.Name)]
public sealed class PollRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_AddedPoll_RoundTripsPollWithThreeParticipants()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var sut = new PollRepository(dataSource);
        var poll = NewPoll();

        await sut.AddAsync(poll, Ct);
        var loaded = await sut.GetAsync(poll.Id, Ct);

        Assert.NotNull(loaded);
        Assert.Equal(poll.Id, loaded.Id);
        Assert.Equal(poll.CreatedAt, loaded.CreatedAt);
        Assert.Null(loaded.OpenedAt);
        Assert.Null(loaded.ClosesAt);
        Assert.False(loaded.IsFinalized);
        Assert.Equal(poll.Participants, loaded.Participants);
    }

    [Fact]
    public async Task GetAsync_UnknownPoll_ReturnsNull()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var sut = new PollRepository(dataSource);

        Assert.Null(await sut.GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task TryOpenAsync_CreatedPoll_PersistsOpenedAtAndClosesAt()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var sut = new PollRepository(dataSource);
        var poll = NewPoll();
        await sut.AddAsync(poll, Ct);
        poll.Open(Now.AddSeconds(1));

        var opened = await sut.TryOpenAsync(poll, Ct);
        var loaded = await sut.GetAsync(poll.Id, Ct);

        Assert.True(opened);
        Assert.Equal(Now.AddSeconds(1), loaded!.OpenedAt);
        Assert.Equal(Now.AddSeconds(31), loaded.ClosesAt);
    }

    [Fact]
    public async Task GetAsync_AfterSimulatedRestart_ReadsSameClosesAt()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync(Ct);
        var poll = NewPoll();
        await using (var first = NpgsqlDataSource.Create(connectionString))
        {
            await new SchemaInitializer(first).ApplyAsync(Ct);
            var repository = new PollRepository(first);
            await repository.AddAsync(poll, Ct);
            poll.Open(Now);
            await repository.TryOpenAsync(poll, Ct);
        }

        await using var second = NpgsqlDataSource.Create(connectionString);
        await new SchemaInitializer(second).ApplyAsync(Ct);
        var loaded = await new PollRepository(second).GetAsync(poll.Id, Ct);

        Assert.Equal(poll.ClosesAt, loaded!.ClosesAt);
        Assert.Equal(Now.AddSeconds(30), loaded.ClosesAt);
    }

    [Fact]
    public async Task TryOpenAsync_TwoPollsConcurrently_OnlyOneWins()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var sut = new PollRepository(dataSource);
        var first = NewPoll();
        var second = NewPoll();
        await sut.AddAsync(first, Ct);
        await sut.AddAsync(second, Ct);
        first.Open(Now);
        second.Open(Now);

        var results = await Task.WhenAll(
            Task.Run(() => sut.TryOpenAsync(first, Ct), Ct),
            Task.Run(() => sut.TryOpenAsync(second, Ct), Ct));

        Assert.Single(results, r => r);
    }

    [Fact]
    public async Task TryOpenAsync_PreviousPollFinalized_Opens()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var sut = new PollRepository(dataSource);
        var first = NewPoll();
        var second = NewPoll();
        await sut.AddAsync(first, Ct);
        await sut.AddAsync(second, Ct);
        first.Open(Now);
        await sut.TryOpenAsync(first, Ct);
        await MarkFinalizedAsync(dataSource, first.Id);
        second.Open(Now.AddMinutes(1));

        Assert.True(await sut.TryOpenAsync(second, Ct));
    }

    [Fact]
    public async Task ListDueForFinalizationAsync_MixedPolls_ReturnsOnlyClosedAndNotFinalized()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var sut = new PollRepository(dataSource);
        var finalized = NewPoll();
        var due = NewPoll();
        var neverOpened = NewPoll();
        foreach (var poll in new[] { finalized, due, neverOpened })
        {
            await sut.AddAsync(poll, Ct);
        }

        finalized.Open(Now);
        await sut.TryOpenAsync(finalized, Ct);
        await MarkFinalizedAsync(dataSource, finalized.Id);
        due.Open(Now.AddMinutes(1));
        await sut.TryOpenAsync(due, Ct);

        var listed = await sut.ListDueForFinalizationAsync(due.ClosesAt!.Value.AddTicks(10), Ct);
        var atClosesAt = await sut.ListDueForFinalizationAsync(due.ClosesAt.Value, Ct);

        Assert.Equal([due.Id], listed.Select(p => p.Id));
        Assert.Empty(atClosesAt);
    }

    private static Poll NewPoll()
    {
        return Poll.Create(["Ana", "Bruno", "Carla"], Now).Value;
    }

    private static async Task MarkFinalizedAsync(NpgsqlDataSource dataSource, Guid pollId)
    {
        await using var command = dataSource.CreateCommand("UPDATE polls SET finalized = true WHERE id = $1");
        command.Parameters.AddWithValue(pollId);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
