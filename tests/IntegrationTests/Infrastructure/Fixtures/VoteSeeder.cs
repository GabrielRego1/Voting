using Domain.Entities;
using Npgsql;

namespace IntegrationTests.Infrastructure.Fixtures;

public static class VoteSeeder
{
    /// <summary>
    /// Inserts <paramref name="counts"/>[i] votes for participant i, received one second after opening.
    /// </summary>
    public static async Task SeedVotesAsync(NpgsqlDataSource dataSource, Poll poll, CancellationToken cancellationToken, params long[] counts)
    {
        var ids = new List<Guid>();
        var participants = new List<Guid>();
        var keys = new List<string>();
        for (var i = 0; i < counts.Length; i++)
        {
            for (var n = 0; n < counts[i]; n++)
            {
                ids.Add(Guid.CreateVersion7());
                participants.Add(poll.Participants[i].Id);
                keys.Add($"seed-{i}-{n}");
            }
        }

        await using var command = dataSource.CreateCommand("""
            INSERT INTO votes (id, poll_id, participant_id, idempotency_key, received_at)
            SELECT id, $1, participant_id, key, $2 FROM unnest($3::uuid[], $4::uuid[], $5::text[]) AS t (id, participant_id, key)
            """);
        command.Parameters.AddWithValue(poll.Id);
        command.Parameters.AddWithValue(poll.OpenedAt!.Value.AddSeconds(1));
        command.Parameters.AddWithValue(ids.ToArray());
        command.Parameters.AddWithValue(participants.ToArray());
        command.Parameters.AddWithValue(keys.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
