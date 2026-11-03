using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Voting.Api.Tests.Fixtures;

/// <summary>
/// Hosts the API against a real PostgreSQL database with a controllable business clock.
/// Every instance gets its own empty database inside a PostgreSQL container shared by the test process,
/// so tests never observe each other's polls, votes or clock.
/// </summary>
public class VotingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly Lazy<Task<PostgreSqlContainer>> SharedContainer = new(StartContainerAsync);

    private readonly Dictionary<string, string?> _settings = [];

    /// <summary>
    /// Business clock used by the API. Advancing it never makes the vote writer wait (research R-04).
    /// </summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Overrides a configuration value. Must be called before the first client is created.
    /// </summary>
    public VotingApiFactory WithSetting(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }

    public async ValueTask InitializeAsync()
    {
        var container = await SharedContainer.Value;
        var name = $"api_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = name }.ConnectionString;
    }

    public async Task<long> CountVotesAsync(Guid pollId)
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        await using var command = dataSource.CreateCommand("SELECT count(*) FROM votes WHERE poll_id = $1");
        command.Parameters.AddWithValue(pollId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("ConnectionStrings:Voting", ConnectionString);
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    private static async Task<PostgreSqlContainer> StartContainerAsync()
    {
        var container = new PostgreSqlBuilder("postgres:17").Build();
        await container.StartAsync();
        return container;
    }
}
