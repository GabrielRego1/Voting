using Application.Abstractions.Persistence;
using Infrastructure.BackgroundServices;
using Infrastructure.Options;
using Infrastructure.Persistence.Migrations;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Persistence.Writers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "Voting";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) => services
        .AddSingleton(_ => NpgsqlDataSource.Create(
            configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.")))
        .AddSingleton<SchemaInitializer>()
        .AddHostedService(sp => sp.GetRequiredService<SchemaInitializer>())
        .AddSingleton<PollRepository>()
        .AddSingleton<IPollRepository>(sp => new CachedPollRepository(
            sp.GetRequiredService<PollRepository>(),
            sp.GetRequiredService<TimeProvider>()))
        .Configure<VoteWriterOptions>(configuration.GetSection(VoteWriterOptions.SectionName))
        .AddSingleton<VoteBatchWriter>()
        .AddSingleton<IVoteWriter>(sp => sp.GetRequiredService<VoteBatchWriter>())
        .AddHostedService(sp => sp.GetRequiredService<VoteBatchWriter>())
        .AddSingleton<IVoteTally, VoteTally>()
        .AddSingleton<IResultRepository, ResultRepository>()

        // Registered after the writer: hosted services start in order, and finalization flushes the writer.
        .AddHostedService<PollFinalizer>();
}
