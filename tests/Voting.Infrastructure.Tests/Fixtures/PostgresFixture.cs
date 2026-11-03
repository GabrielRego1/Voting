using Testcontainers.PostgreSql;

namespace Voting.Infrastructure.Tests.Fixtures;

/// <summary>
/// Starts a single PostgreSQL container shared by every test in <see cref="PostgresCollection"/>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
