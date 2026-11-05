using Application.Abstractions.Persistence;
using Domain.Entities;
using Infrastructure.Persistence.Repositories;
using IntegrationTests.Infrastructure.Fixtures;
using Microsoft.Extensions.Time.Testing;

namespace IntegrationTests.Infrastructure.Persistence.Repositories;

[Collection(PostgresCollection.Name)]
public sealed class CachedPollRepositoryTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_OpenPollReadTwice_HitsDatabaseOnce()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        var inner = new CountingPollRepository(new PollRepository(dataSource));
        var sut = new CachedPollRepository(inner, new FakeTimeProvider(PollSeeder.OpenedAt.AddSeconds(5)));

        var first = await sut.GetAsync(poll.Id, Ct);
        var second = await sut.GetAsync(poll.Id, Ct);
        var third = await sut.GetAsync(poll.Id, Ct);

        Assert.Equal(poll.ClosesAt, first!.ClosesAt);
        Assert.Same(first, second);
        Assert.Same(first, third);
        Assert.Equal(1, inner.GetCalls);
    }

    [Fact]
    public async Task TryOpenAsync_CachedPoll_InvalidatesSnapshot()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        var inner = new CountingPollRepository(new PollRepository(dataSource));
        var sut = new CachedPollRepository(inner, new FakeTimeProvider(PollSeeder.OpenedAt.AddSeconds(5)));
        var cached = await sut.GetAsync(poll.Id, Ct);

        await sut.TryOpenAsync(cached!, Ct);
        await sut.GetAsync(poll.Id, Ct);

        Assert.Equal(2, inner.GetCalls);
    }

    [Fact]
    public async Task GetAsync_PollNotOpened_IsNotCached()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var created = Poll.Create(["Ana", "Bruno", "Carla"], PollSeeder.OpenedAt).Value;
        var inner = new CountingPollRepository(new PollRepository(dataSource));
        await inner.AddAsync(created, Ct);
        var sut = new CachedPollRepository(inner, new FakeTimeProvider(PollSeeder.OpenedAt));

        await sut.GetAsync(created.Id, Ct);
        await sut.GetAsync(created.Id, Ct);

        Assert.Equal(2, inner.GetCalls);
    }

    [Fact]
    public async Task GetAsync_ClosedButNotFinalized_RereadsDatabase()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        var inner = new CountingPollRepository(new PollRepository(dataSource));
        var time = new FakeTimeProvider(PollSeeder.OpenedAt.AddSeconds(5));
        var sut = new CachedPollRepository(inner, time);
        await sut.GetAsync(poll.Id, Ct);
        time.Advance(TimeSpan.FromSeconds(60));

        await sut.GetAsync(poll.Id, Ct);
        await sut.GetAsync(poll.Id, Ct);

        Assert.Equal(3, inner.GetCalls);
    }

    private sealed class CountingPollRepository(IPollRepository inner) : IPollRepository
    {
        private int _getCalls;

        public int GetCalls => _getCalls;

        public Task AddAsync(Poll poll, CancellationToken cancellationToken = default)
        {
            return inner.AddAsync(poll, cancellationToken);
        }

        public Task<Poll?> GetAsync(Guid pollId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _getCalls);
            return inner.GetAsync(pollId, cancellationToken);
        }

        public Task<bool> TryOpenAsync(Poll poll, CancellationToken cancellationToken = default)
        {
            return inner.TryOpenAsync(poll, cancellationToken);
        }

        public Task<IReadOnlyList<Poll>> ListDueForFinalizationAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            return inner.ListDueForFinalizationAsync(now, cancellationToken);
        }
    }
}
