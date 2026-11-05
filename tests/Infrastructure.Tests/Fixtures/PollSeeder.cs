using Npgsql;
using Domain.Polls;
using Infrastructure.Persistence;

namespace Infrastructure.Tests.Fixtures;

public static class PollSeeder
{
    public static readonly DateTimeOffset OpenedAt = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Persists a poll with participants Ana, Bruno and Carla, opened at <see cref="OpenedAt"/>.
    /// </summary>
    public static async Task<Poll> SeedOpenPollAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var repository = new PollRepository(dataSource);
        var poll = Poll.Create(["Ana", "Bruno", "Carla"], OpenedAt).Value;
        await repository.AddAsync(poll, cancellationToken);
        poll.Open(OpenedAt);
        if (!await repository.TryOpenAsync(poll, cancellationToken))
        {
            throw new InvalidOperationException("Could not open the seeded poll.");
        }

        return poll;
    }

    public static async Task<long> CountVotesAsync(NpgsqlDataSource dataSource, Guid pollId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT count(*) FROM votes WHERE poll_id = $1");
        command.Parameters.AddWithValue(pollId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
