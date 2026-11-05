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
    public const string ConnectionStringName = "Voting";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            return NpgsqlDataSource.Create(connectionString);
        });

        services.AddSingleton<SchemaInitializer>();
        services.AddHostedService(sp => sp.GetRequiredService<SchemaInitializer>());
        services.AddSingleton<PollRepository>();
        services.AddSingleton<IPollRepository>(sp => new CachedPollRepository(
            sp.GetRequiredService<PollRepository>(),
            sp.GetRequiredService<TimeProvider>()));

        services.AddOptions<VoteWriterOptions>().Bind(configuration.GetSection(VoteWriterOptions.SectionName));
        services.AddSingleton<VoteBatchWriter>();
        services.AddSingleton<IVoteWriter>(sp => sp.GetRequiredService<VoteBatchWriter>());
        services.AddHostedService(sp => sp.GetRequiredService<VoteBatchWriter>());

        services.AddSingleton<IVoteTally, VoteTally>();
        services.AddSingleton<IResultRepository, ResultRepository>();

        // Registered after the writer: hosted services start in order, and finalization flushes the writer.
        services.AddHostedService<PollFinalizer>();
        return services;
    }
}
