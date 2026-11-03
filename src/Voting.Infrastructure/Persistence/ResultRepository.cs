using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Voting.Application.Abstractions;
using Voting.Domain.Results;

namespace Voting.Infrastructure.Persistence;

/// <summary>
/// Stores each poll result exactly once, marking the poll as finalized in the same transaction.
/// Tied participants are not stored: they are derived from the tallies (highest count) when the outcome is a tie.
/// </summary>
public sealed class ResultRepository(NpgsqlDataSource dataSource) : IResultRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> TryAddAsync(PollResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO poll_results (poll_id, total_votes, outcome, eliminated_participant_id, tallies, finalized_at)
            VALUES ($1, $2, $3, $4, $5, $6)
            ON CONFLICT (poll_id) DO NOTHING
            """,
            connection,
            transaction))
        {
            insert.Parameters.AddWithValue(result.PollId);
            insert.Parameters.AddWithValue(result.TotalVotes);
            insert.Parameters.AddWithValue(result.Outcome.ToString());
            insert.Parameters.AddWithValue((object?)result.EliminatedParticipantId ?? DBNull.Value);
            insert.Parameters.Add(new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Jsonb,
                Value = JsonSerializer.Serialize(result.Tallies, JsonOptions),
            });
            insert.Parameters.AddWithValue(result.FinalizedAt);
            if (await insert.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                return false;
            }
        }

        await using (var finalize = new NpgsqlCommand("UPDATE polls SET finalized = true WHERE id = $1", connection, transaction))
        {
            finalize.Parameters.AddWithValue(result.PollId);
            await finalize.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<PollResult?> GetAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT total_votes, outcome, eliminated_participant_id, tallies::text, finalized_at
            FROM poll_results
            WHERE poll_id = $1
            """);
        command.Parameters.AddWithValue(pollId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var outcome = Enum.Parse<Outcome>(reader.GetString(1));
        var tallies = JsonSerializer.Deserialize<ParticipantTally[]>(reader.GetString(3), JsonOptions)
            ?? throw new InvalidOperationException($"Result of poll {pollId} has no tallies.");
        IReadOnlyList<Guid> tied = outcome == Outcome.Tie
            ? tallies.Where(t => t.Votes == tallies.Max(x => x.Votes)).Select(t => t.ParticipantId).ToArray()
            : [];

        return new PollResult(
            pollId,
            reader.GetInt64(0),
            tallies,
            outcome,
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            tied,
            reader.GetFieldValue<DateTimeOffset>(4));
    }
}
