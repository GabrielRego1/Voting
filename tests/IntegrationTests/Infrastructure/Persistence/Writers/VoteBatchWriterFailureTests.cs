using Infrastructure.Options;
using Infrastructure.Persistence.Migrations;
using IntegrationTests.Infrastructure.Fixtures;
using Npgsql;
using Testcontainers.PostgreSql;

namespace IntegrationTests.Infrastructure.Persistence.Writers;

/// <summary>
/// Uses a dedicated container (outside <see cref="PostgresCollection"/>) because it pauses the database.
/// </summary>
public sealed class VoteBatchWriterFailureTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task WriteAsync_DatabasePausedDuringBatch_FailsEveryVoteOfTheBatchAndConfirmsNone()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Timeout = 2,
            CommandTimeout = 2,
            CancellationTimeout = 1000,
        }.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await new SchemaInitializer(dataSource).ApplyAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        using var writer = VoteBatchWriterTests.CreateWriter(
            dataSource,
            new VoteWriterOptions { MaxBatchDelay = TimeSpan.FromMilliseconds(500) });
        await writer.StartAsync(Ct);

        var writes = Enumerable.Range(0, 20)
            .Select(i => writer.WriteAsync(VoteBatchWriterTests.NewVote(poll, i % 3, $"paused-{i}"), Ct))
            .ToArray();
        await _container.PauseAsync(Ct);
        try
        {
            var all = Task.WhenAll(writes);
            await Assert.ThrowsAnyAsync<Exception>(() => all.WaitAsync(TimeSpan.FromSeconds(30), Ct));
            Assert.All(writes, w => Assert.True(w.IsFaulted));
        }
        finally
        {
            await _container.UnpauseAsync(Ct);
        }

        await writer.StopAsync(Ct);
        await using var fresh = NpgsqlDataSource.Create(_container.GetConnectionString());
        Assert.Equal(0, await PollSeeder.CountVotesAsync(fresh, poll.Id, Ct));
    }
}
