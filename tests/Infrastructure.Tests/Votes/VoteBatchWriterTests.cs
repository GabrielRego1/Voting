using Application;
using Application.Abstractions.Persistence;
using Application.Dtos;
using Domain.Entities;
using Infrastructure.Options;
using Infrastructure.Persistence.Migrations;
using Infrastructure.Persistence.Writers;
using Infrastructure.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Infrastructure.Tests.Votes;

[Collection(PostgresCollection.Name)]
public sealed class VoteBatchWriterTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WriteAsync_Accepted_RowAlreadyExistsInVotes()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        await using var writer = await StartWriterAsync(dataSource);
        var vote = NewVote(poll, 0, "k1");

        var outcome = await writer.WriteAsync(vote, Ct);

        Assert.Equal(VoteWriteStatus.Accepted, outcome.Status);
        Assert.Equal(VoteReceipt.From(vote), outcome.Receipt);
        await using var command = dataSource.CreateCommand(
            "SELECT participant_id, idempotency_key, received_at FROM votes WHERE id = $1");
        command.Parameters.AddWithValue(vote.Id);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(vote.ParticipantId, reader.GetGuid(0));
        Assert.Equal("k1", reader.GetString(1));
        Assert.Equal(vote.ReceivedAt, reader.GetFieldValue<DateTimeOffset>(2));
    }

    [Fact]
    public async Task WriteAsync_FiveThousandConcurrentWrites_AllAcceptedAndPersisted()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        await using var writer = await StartWriterAsync(dataSource);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5000)
            .Select(i => Task.Run(() => writer.WriteAsync(NewVote(poll, i % 3, $"key-{i}"), Ct), Ct)));

        Assert.Equal(5000, outcomes.Count(o => o.Status == VoteWriteStatus.Accepted));
        Assert.Equal(5000, await PollSeeder.CountVotesAsync(dataSource, poll.Id, Ct));
    }

    [Fact]
    public async Task WriteAsync_SameKeySameParticipant_ReplaysOriginalReceipt()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        await using var writer = await StartWriterAsync(dataSource);
        var original = NewVote(poll, 1, "k1");
        await writer.WriteAsync(original, Ct);

        var outcome = await writer.WriteAsync(NewVote(poll, 1, "k1", seconds: 5), Ct);

        Assert.Equal(VoteWriteStatus.Replayed, outcome.Status);
        Assert.Equal(original.Id, outcome.Receipt!.VoteId);
        Assert.Equal(original.ReceivedAt, outcome.Receipt.ReceivedAt);
        Assert.Equal(1, await PollSeeder.CountVotesAsync(dataSource, poll.Id, Ct));
    }

    [Fact]
    public async Task WriteAsync_SameKeyDifferentParticipant_ReturnsConflictAndKeepsOriginal()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        await using var writer = await StartWriterAsync(dataSource);
        var original = NewVote(poll, 0, "k1");
        await writer.WriteAsync(original, Ct);

        var outcome = await writer.WriteAsync(NewVote(poll, 2, "k1"), Ct);

        Assert.Equal(VoteWriteStatus.Conflict, outcome.Status);
        await using var command = dataSource.CreateCommand("SELECT id, participant_id FROM votes WHERE poll_id = $1");
        command.Parameters.AddWithValue(poll.Id);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(original.Id, reader.GetGuid(0));
        Assert.Equal(original.ParticipantId, reader.GetGuid(1));
        Assert.False(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task WriteAsync_SameKeyTwiceInSameBatch_FirstAcceptedSecondReplayedOrConflict()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        var writer = CreateWriter(dataSource, new VoteWriterOptions { MaxBatchDelay = TimeSpan.FromMilliseconds(200) });
        var first = writer.WriteAsync(NewVote(poll, 0, "dup"), Ct);
        var sameParticipant = writer.WriteAsync(NewVote(poll, 0, "dup"), Ct);
        var otherParticipant = writer.WriteAsync(NewVote(poll, 1, "dup"), Ct);
        await writer.StartAsync(Ct);

        try
        {
            var outcomes = await Task.WhenAll(first, sameParticipant, otherParticipant);

            Assert.Equal(VoteWriteStatus.Accepted, outcomes[0].Status);
            Assert.Equal(VoteWriteStatus.Replayed, outcomes[1].Status);
            Assert.Equal(outcomes[0].Receipt!.VoteId, outcomes[1].Receipt!.VoteId);
            Assert.Equal(VoteWriteStatus.Conflict, outcomes[2].Status);
            Assert.Equal(1, await PollSeeder.CountVotesAsync(dataSource, poll.Id, Ct));
        }
        finally
        {
            await writer.StopAsync(Ct);
            writer.Dispose();
        }
    }

    [Fact]
    public async Task WriteAsync_QueueFullWhileWriterStopped_ReturnsOverloaded()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        using var writer = CreateWriter(dataSource, new VoteWriterOptions { Capacity = 1 });

        var queued = writer.WriteAsync(NewVote(poll, 0, "k1"), Ct);
        var overflow = await writer.WriteAsync(NewVote(poll, 0, "k2"), Ct);

        Assert.Equal(VoteWriteStatus.Overloaded, overflow.Status);
        Assert.False(queued.IsCompleted);
    }

    [Fact]
    public async Task WriteAsync_FakeTimeProviderNeverAdvanced_StillWritesBatches()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync(Ct);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await new SchemaInitializer(dataSource).ApplyAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Voting"] = connectionString })
            .Build();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(PollSeeder.OpenedAt))
            .AddApplication()
            .AddInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        foreach (var hosted in hostedServices)
        {
            await hosted.StartAsync(Ct);
        }

        try
        {
            var writer = provider.GetRequiredService<IVoteWriter>();
            var outcomes = await Task.WhenAll(Enumerable.Range(0, 10)
                .Select(i => writer.WriteAsync(NewVote(poll, 0, $"fake-{i}"), Ct)))
                .WaitAsync(TimeSpan.FromSeconds(10), Ct);

            Assert.All(outcomes, o => Assert.Equal(VoteWriteStatus.Accepted, o.Status));
        }
        finally
        {
            foreach (var hosted in hostedServices.Reverse())
            {
                await hosted.StopAsync(Ct);
            }
        }
    }

    [Fact]
    public async Task FlushUntilAsync_QueuedVotes_ReturnsOnlyAfterAllAreCommitted()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        await using var writer = await StartWriterAsync(
            dataSource,
            new VoteWriterOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(300) });
        var writes = Enumerable.Range(0, 55)
            .Select(i => writer.WriteAsync(NewVote(poll, i % 3, $"flush-{i}", seconds: 29), Ct))
            .ToArray();

        await writer.FlushUntilAsync(poll.ClosesAt!.Value, Ct);

        Assert.Equal(55, await PollSeeder.CountVotesAsync(dataSource, poll.Id, Ct));
        Assert.All(writes, w => Assert.True(w.IsCompletedSuccessfully));
    }

    internal static Vote NewVote(Poll poll, int participantIndex, string key, int seconds = 1)
    {
        return Vote.Create(poll.Id, poll.Participants[participantIndex].Id, key, PollSeeder.OpenedAt.AddSeconds(seconds));
    }

    internal static VoteBatchWriter CreateWriter(NpgsqlDataSource dataSource, VoteWriterOptions? options = null)
    {
        return new VoteBatchWriter(dataSource, MsOptions.Create(options ?? new VoteWriterOptions()), NullLogger<VoteBatchWriter>.Instance);
    }

    private static async Task<RunningWriter> StartWriterAsync(NpgsqlDataSource dataSource, VoteWriterOptions? options = null)
    {
        var writer = CreateWriter(dataSource, options);
        await writer.StartAsync(Ct);
        return new RunningWriter(writer);
    }

    internal sealed class RunningWriter(VoteBatchWriter writer) : IAsyncDisposable
    {
        public Task<VoteWriteOutcome> WriteAsync(Vote vote, CancellationToken cancellationToken)
        {
            return writer.WriteAsync(vote, cancellationToken);
        }

        public Task FlushUntilAsync(DateTimeOffset instant, CancellationToken cancellationToken)
        {
            return writer.FlushUntilAsync(instant, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await writer.StopAsync(CancellationToken.None);
            writer.Dispose();
        }
    }
}
