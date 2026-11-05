using Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Infrastructure.Tests.Fixtures;

/// <summary>
/// Starts a single PostgreSQL container shared by every test in <see cref="PostgresCollection"/>.
/// Tests isolate themselves by creating a fresh database with <see cref="CreateDatabaseAsync"/>,
/// because votes are append-only and cannot be cleaned up between tests.
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

    /// <summary>
    /// Creates an empty database with the schema applied and returns a data source for it.
    /// </summary>
    public async Task<NpgsqlDataSource> CreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = await CreateEmptyDatabaseAsync(cancellationToken);
        var dataSource = NpgsqlDataSource.Create(connectionString);
        await new SchemaInitializer(dataSource).ApplyAsync(cancellationToken);
        return dataSource;
    }

    /// <summary>
    /// Creates an empty database (no schema) and returns its connection string.
    /// </summary>
    public async Task<string> CreateEmptyDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var name = $"test_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }
}
