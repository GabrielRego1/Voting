using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.WebApi.Fixtures;

namespace IntegrationTests.WebApi.Endpoints;

public sealed class VotingWindowTests : ApiTest
{
    [Fact]
    public async Task CastVote_BeforeOpening_Returns409VotingNotOpen()
    {
        var poll = await CreatePollAsync();

        var response = await VoteAsync(PollId(poll), ParticipantId(poll, 0), "early");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("voting_not_open", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CastVote_TenSecondsAfterOpening_Returns201()
    {
        var poll = await CreateAndOpenPollAsync();
        Api.Time.Advance(TimeSpan.FromSeconds(10));

        var response = await VoteAsync(PollId(poll), ParticipantId(poll, 0), "at-10s");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CastVote_ExactlyAtClosesAt_Returns201()
    {
        var poll = await CreateAndOpenPollAsync();
        Api.Time.SetUtcNow(poll.GetProperty("closesAt").GetDateTimeOffset());

        var response = await VoteAsync(PollId(poll), ParticipantId(poll, 0), "at-closes-at");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CastVote_OneMillisecondAfterClosesAt_Returns409VotingClosedAndRecordsNothing()
    {
        var poll = await CreateAndOpenPollAsync();
        await VoteAsync(PollId(poll), ParticipantId(poll, 1), "in-time");
        var before = await Api.CountVotesAsync(PollId(poll));
        Api.Time.SetUtcNow(poll.GetProperty("closesAt").GetDateTimeOffset().AddMilliseconds(1));

        var response = await VoteAsync(PollId(poll), ParticipantId(poll, 0), "too-late");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("voting_closed", await ReadProblemCodeAsync(response));
        Assert.Equal(before, await Api.CountVotesAsync(PollId(poll)));
        Assert.Equal(1, before);
    }

    [Fact]
    public async Task GetPoll_AfterClosesAt_ReturnsClosedStatus()
    {
        var poll = await CreateAndOpenPollAsync();
        Api.Time.SetUtcNow(poll.GetProperty("closesAt").GetDateTimeOffset().AddMilliseconds(1));

        var response = await Client.GetAsync($"/polls/{PollId(poll)}", Ct);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Closed", body.GetProperty("status").GetString());
    }
}
