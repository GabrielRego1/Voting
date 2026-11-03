using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Voting.Application.Abstractions;
using Voting.Infrastructure.Persistence;

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
        services.AddSingleton<IPollRepository, PollRepository>();
        return services;
    }
}
