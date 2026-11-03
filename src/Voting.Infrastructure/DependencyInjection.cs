using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Voting.Application.Abstractions;
using Voting.Infrastructure.Persistence;
using Voting.Infrastructure.Votes;

namespace Voting.Infrastructure;

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
        return services;
    }
}
