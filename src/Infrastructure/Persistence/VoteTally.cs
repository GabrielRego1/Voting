using Application.Abstractions.Persistence;
using Npgsql;

namespace Infrastructure.Persistence;

public sealed class VoteTally(NpgsqlDataSource dataSource) : IVoteTally
{
    public async Task<IReadOnlyDictionary<Guid, long>> CountByParticipantAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT p.id, count(v.id)
            FROM participants p
            LEFT JOIN votes v ON v.poll_id = p.poll_id AND v.participant_id = p.id
            WHERE p.poll_id = $1
            GROUP BY p.id
            """);
        command.Parameters.AddWithValue(pollId);

        var counts = new Dictionary<Guid, long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts[reader.GetGuid(0)] = reader.GetInt64(1);
        }

        return counts;
    }
}
