using Application;
using Application.Abstractions.Persistence;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Migrations;
using Infrastructure.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Infrastructure.Tests.Hosting;

[Collection(PostgresCollection.Name)]
public sealed class PollFinalizerTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Finalizer_ClockPassesClosesAt_FinalizesWithinFiveSeconds()
    {
        var (connectionString, poll) = await SeedAsync();
        var time = new FakeTimeProvider(PollSeeder.OpenedAt.AddSeconds(5));
        await using var host = await StartAsync(connectionString, time);
        var writer = host.Services.GetRequiredService<IVoteWriter>();
        await Task.WhenAll(Enumerable.Range(0, 30)
            .Select(i => writer.WriteAsync(Vote.Create(poll.Id, poll.Participants[i % 2].Id, $"k{i}", time.GetUtcNow()), Ct)));

        time.SetUtcNow(poll.ClosesAt!.Value.AddMilliseconds(300));
        var result = await WaitForResultAsync(host.Services, poll.Id);

        Assert.Equal(30, result.TotalVotes);
        Assert.Equal(Outcome.Tie, result.Outcome);
        Assert.True(result.FinalizedAt > poll.ClosesAt);
        Assert.True(result.FinalizedAt - poll.ClosesAt <= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Finalizer_StartsAfterClosesAtWithoutResult_FinalizesOnStartup()
    {
        var (connectionString, poll) = await SeedAsync(600, 300, 100);
        var time = new FakeTimeProvider(poll.ClosesAt!.Value.AddMinutes(10));

        await using var host = await StartAsync(connectionString, time);
        var result = await WaitForResultAsync(host.Services, poll.Id);

        Assert.Equal(1000, result.TotalVotes);
        Assert.Equal(poll.Participants[0].Id, result.EliminatedParticipantId);
        Assert.Equal(time.GetUtcNow(), result.FinalizedAt);
    }

    [Fact]
    public async Task Finalizer_ClockReachesExactlyClosesAt_DoesNotFinalize()
    {
        var (connectionString, poll) = await SeedAsync(1, 0, 0);
        var time = new FakeTimeProvider(poll.ClosesAt!.Value.AddMilliseconds(-250));

        await using var host = await StartAsync(connectionString, time);
        time.Advance(TimeSpan.FromMilliseconds(250));
        await Task.Delay(500, Ct);

        Assert.Null(await host.Services.GetRequiredService<IResultRepository>().GetAsync(poll.Id, Ct));
    }

    private async Task<(string ConnectionString, Poll Poll)> SeedAsync(params long[] counts)
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync(Ct);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await new SchemaInitializer(dataSource).ApplyAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        if (counts.Length > 0)
        {
            await VoteSeeder.SeedVotesAsync(dataSource, poll, Ct, counts);
        }

        return (connectionString, poll);
    }

    private static async Task<RunningServices> StartAsync(string connectionString, TimeProvider time)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Voting"] = connectionString })
            .Build();
        var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(time)
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
        var running = new RunningServices(provider);
        await running.StartAsync();
        return running;
    }

    private static async Task<PollResult> WaitForResultAsync(IServiceProvider services, Guid pollId)
    {
        var results = services.GetRequiredService<IResultRepository>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var result = await results.GetAsync(pollId, timeout.Token);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private sealed class RunningServices(ServiceProvider provider) : IAsyncDisposable
    {
        private readonly IHostedService[] _hosted = provider.GetServices<IHostedService>().ToArray();

        public IServiceProvider Services => provider;

        public async Task StartAsync()
        {
            foreach (var hosted in _hosted)
            {
                await hosted.StartAsync(Ct);
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var hosted in _hosted.Reverse())
            {
                await hosted.StopAsync(CancellationToken.None);
            }

            await provider.DisposeAsync();
        }
    }
}
