using Application.Abstractions.UseCases;
using Application.Dtos;
using Application.UseCases.CastVote;
using Application.UseCases.GetPollResult;
using Application;
using Domain.Abstractions.Services;
using Domain.Common;
using Domain.Entities;
using Infrastructure;
using IntegrationTests.Infrastructure.Fixtures;
using IntegrationTests.WebApi.Fixtures;
using Npgsql;
using WebApi;
using Workload;

namespace IntegrationTests.Infrastructure.Persistence.Migrations;

[Collection(PostgresCollection.Name)]
public sealed class VoteImmutabilityTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("UPDATE votes SET idempotency_key = 'changed' WHERE poll_id = $1")]
    [InlineData("DELETE FROM votes WHERE poll_id = $1")]
    public async Task ModifyVotes_AnyUpdateOrDelete_IsRejectedByAppendOnlyTrigger(string sql)
    {
        await using var dataSource = await postgres.CreateDatabaseAsync(Ct);
        var poll = await PollSeeder.SeedOpenPollAsync(dataSource, Ct);
        await using (var insert = dataSource.CreateCommand(
            "INSERT INTO votes (id, poll_id, participant_id, idempotency_key, received_at) VALUES ($1, $2, $3, 'k1', $4)"))
        {
            insert.Parameters.AddWithValue(Guid.CreateVersion7());
            insert.Parameters.AddWithValue(poll.Id);
            insert.Parameters.AddWithValue(poll.Participants[0].Id);
            insert.Parameters.AddWithValue(PollSeeder.OpenedAt);
            await insert.ExecuteNonQueryAsync(Ct);
        }

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(poll.Id);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));

        Assert.Contains("votes_append_only", ex.MessageText, StringComparison.Ordinal);
        Assert.Equal(1, await PollSeeder.CountVotesAsync(dataSource, poll.Id, Ct));
    }
}
