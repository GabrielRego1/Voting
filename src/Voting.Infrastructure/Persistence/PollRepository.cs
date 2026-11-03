using Npgsql;
using Voting.Application.Abstractions;
using Voting.Domain.Polls;

namespace Voting.Infrastructure.Persistence;

public sealed class PollRepository(NpgsqlDataSource dataSource) : IPollRepository
{
    private const string UniqueViolation = "23505";
    private const string OneOpenPollIndex = "one_open_poll";

    private const string SelectPollsSql = """
        SELECT p.id, p.created_at, p.opened_at, p.closes_at, p.finalized, pa.id, pa.name, pa.position
        FROM polls p
        JOIN participants pa ON pa.poll_id = p.id
        """;

    public async Task AddAsync(Poll poll, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(poll);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insertPoll = new NpgsqlCommand(
            "INSERT INTO polls (id, created_at, opened_at, closes_at, finalized) VALUES ($1, $2, $3, $4, $5)",
            connection,
            transaction))
        {
            insertPoll.Parameters.AddWithValue(poll.Id);
            insertPoll.Parameters.AddWithValue(poll.CreatedAt);
            insertPoll.Parameters.AddWithValue((object?)poll.OpenedAt ?? DBNull.Value);
            insertPoll.Parameters.AddWithValue((object?)poll.ClosesAt ?? DBNull.Value);
            insertPoll.Parameters.AddWithValue(poll.IsFinalized);
            await insertPoll.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertParticipants = new NpgsqlCommand(
            """
            INSERT INTO participants (id, poll_id, name, position)
            SELECT id, $1, name, position FROM unnest($2::uuid[], $3::text[], $4::smallint[]) AS t (id, name, position)
            """,
            connection,
            transaction))
        {
            insertParticipants.Parameters.AddWithValue(poll.Id);
            insertParticipants.Parameters.AddWithValue(poll.Participants.Select(p => p.Id).ToArray());
            insertParticipants.Parameters.AddWithValue(poll.Participants.Select(p => p.Name).ToArray());
            insertParticipants.Parameters.AddWithValue(poll.Participants.Select(p => (short)p.Position).ToArray());
            await insertParticipants.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Poll?> GetAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(SelectPollsSql + " WHERE p.id = $1");
        command.Parameters.AddWithValue(pollId);
        var polls = await ReadPollsAsync(command, cancellationToken);
        return polls.Count == 0 ? null : polls[0];
    }

    public async Task<bool> TryOpenAsync(Poll poll, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(poll);
        await using var command = dataSource.CreateCommand(
            "UPDATE polls SET opened_at = $2, closes_at = $3 WHERE id = $1 AND opened_at IS NULL");
        command.Parameters.AddWithValue(poll.Id);
        command.Parameters.AddWithValue(poll.OpenedAt ?? throw new ArgumentException("The poll has not been opened.", nameof(poll)));
        command.Parameters.AddWithValue(poll.ClosesAt!.Value);
        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation && ex.ConstraintName == OneOpenPollIndex)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<Poll>> ListDueForFinalizationAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            SelectPollsSql + " WHERE p.closes_at < $1 AND p.finalized = false");
        command.Parameters.AddWithValue(now);
        return await ReadPollsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<Poll>> ReadPollsAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var rows = new List<(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? OpenedAt, DateTimeOffset? ClosesAt, bool Finalized, Participant Participant)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((
                    reader.GetGuid(0),
                    reader.GetFieldValue<DateTimeOffset>(1),
                    reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                    reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
                    reader.GetBoolean(4),
                    new Participant(reader.GetGuid(5), reader.GetString(6), reader.GetInt16(7))));
            }
        }

        return rows
            .GroupBy(r => r.Id)
            .Select(g =>
            {
                var first = g.First();
                return Poll.Rehydrate(
                    first.Id,
                    first.CreatedAt,
                    first.OpenedAt,
                    first.ClosesAt,
                    first.Finalized,
                    g.Select(r => r.Participant).ToArray());
            })
            .ToArray();
    }
}
