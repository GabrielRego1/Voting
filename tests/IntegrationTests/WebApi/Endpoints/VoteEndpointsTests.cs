using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.WebApi.Fixtures;

namespace IntegrationTests.WebApi.Endpoints;

public sealed class VoteEndpointsTests : ApiTest
{
    [Fact]
    public async Task CastVote_NewKey_Returns201WithReceipt()
    {
        var poll = await CreateAndOpenPollAsync();
        var participantId = ParticipantId(poll, 0);

        var response = await VoteAsync(PollId(poll), participantId, "k1");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.NotEqual(Guid.Empty, receipt.GetProperty("voteId").GetGuid());
        Assert.Equal(PollId(poll), receipt.GetProperty("pollId").GetGuid());
        Assert.Equal(participantId, receipt.GetProperty("participantId").GetGuid());
        Assert.Equal(Api.Time.GetUtcNow(), receipt.GetProperty("receivedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task CastVote_RepeatedKeySameParticipant_Returns200WithSameVoteId()
    {
        var poll = await CreateAndOpenPollAsync();
        var first = await VoteAsync(PollId(poll), ParticipantId(poll, 0), "k1");
        var firstReceipt = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);

        var second = await VoteAsync(PollId(poll), ParticipantId(poll, 0), "k1");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondReceipt = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(firstReceipt.GetProperty("voteId").GetGuid(), secondReceipt.GetProperty("voteId").GetGuid());
        Assert.Equal(1, await Api.CountVotesAsync(PollId(poll)));
    }

    [Fact]
    public async Task CastVote_RepeatedKeyOtherParticipant_Returns409IdempotencyKeyConflict()
    {
        var poll = await CreateAndOpenPollAsync();
        await VoteAsync(PollId(poll), ParticipantId(poll, 0), "k1");

        var response = await VoteAsync(PollId(poll), ParticipantId(poll, 1), "k1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("idempotency_key_conflict", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CastVote_UnknownParticipant_Returns422InvalidParticipant()
    {
        var poll = await CreateAndOpenPollAsync();

        var response = await VoteAsync(PollId(poll), Guid.NewGuid(), "k1");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid_participant", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CastVote_MissingKey_Returns400MissingIdempotencyKey()
    {
        var poll = await CreateAndOpenPollAsync();

        var response = await VoteAsync(PollId(poll), ParticipantId(poll, 0), null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("missing_idempotency_key", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CastVote_KeyWith65Characters_Returns400InvalidIdempotencyKey()
    {
        var poll = await CreateAndOpenPollAsync();

        var response = await VoteAsync(PollId(poll), ParticipantId(poll, 0), new string('k', 65));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_idempotency_key", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CastVote_UnknownPoll_Returns404PollNotFound()
    {
        var response = await VoteAsync(Guid.NewGuid(), Guid.NewGuid(), "k1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("poll_not_found", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CastVote_FiveDistinctKeysSameParticipant_RecordsFiveVotes()
    {
        var poll = await CreateAndOpenPollAsync();

        for (var i = 0; i < 5; i++)
        {
            var response = await VoteAsync(PollId(poll), ParticipantId(poll, 2), $"same-voter-{i}");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        Assert.Equal(5, await Api.CountVotesAsync(PollId(poll)));
    }

    [Fact]
    public async Task CastVote_WriterQueueFull_Returns503OverloadedWithRetryAfter()
    {
        await using var api = new VotingApiFactory()
            .WithSetting("VoteWriter:Capacity", "1")
            .WithSetting("VoteWriter:MaxBatchDelay", "00:00:00.300");
        await api.InitializeAsync();
        using var client = api.CreateClient();
        var created = await client.PostAsJsonAsync("/polls", new { participants = new[] { "Ana", "Bruno", "Carla" } }, Ct);
        var poll = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        (await client.PostAsync($"/polls/{PollId(poll)}/open", null, Ct)).EnsureSuccessStatusCode();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => VoteAsync(client, PollId(poll), ParticipantId(poll, 0), $"burst-{i}")));

        var overloaded = responses.Where(r => r.StatusCode == HttpStatusCode.ServiceUnavailable).ToArray();
        Assert.NotEmpty(overloaded);
        foreach (var response in overloaded)
        {
            Assert.True(response.Headers.RetryAfter is not null, "503 must carry Retry-After");
            Assert.Equal("overloaded", await ReadProblemCodeAsync(response));
        }

        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.ServiceUnavailable }));
    }
}
