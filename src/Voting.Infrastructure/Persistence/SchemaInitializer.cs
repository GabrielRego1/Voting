using System.Reflection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Voting.Infrastructure.Persistence;

/// <summary>
/// Applies the embedded <c>Sql/NNN_*.sql</c> scripts, in order, when the host starts.
/// </summary>
public sealed class SchemaInitializer(NpgsqlDataSource dataSource) : IHostedService
{
    // Arbitrary key that serializes concurrent schema initializations.
    private const long SchemaLockKey = 0x766F74696E67;

    private const string ScriptPrefix = "Voting.Infrastructure.Persistence.Sql.";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return ApplyAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue(SchemaLockKey);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var script in LoadScripts())
        {
            await using var command = new NpgsqlCommand(script, connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static IEnumerable<string> LoadScripts()
    {
        var assembly = typeof(SchemaInitializer).Assembly;
        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(ScriptPrefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            yield return ReadResource(assembly, name);
        }
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded script '{name}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
