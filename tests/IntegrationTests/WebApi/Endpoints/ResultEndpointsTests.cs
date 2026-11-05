using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.WebApi.Fixtures;

namespace IntegrationTests.WebApi.Endpoints;

public sealed class ResultEndpointsTests : ApiTest
{
    [Fact]
    public async Task GetResult_PollOpen_Returns409ResultNotAvailable()
    {
        var poll = await CreateAndOpenPollAsync();

        var response = await Client.GetAsync($"/polls/{PollId(poll)}/result", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("result_not_available", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task GetResult_600_300_100_ReturnsEliminatedFirstWithPercentages()
    {
        var poll = await CreateAndOpenPollAsync();
        await SendVotesAsync(poll, 600, 300, 100);

        var result = await CloseAndWaitForResultAsync(poll);

        Assert.Equal(PollId(poll), result.GetProperty("pollId").GetGuid());
        Assert.Equal(1000, result.GetProperty("totalVotes").GetInt64());
        var tallies = result.GetProperty("tallies").EnumerateArray().ToArray();
        Assert.Equal([600L, 300L, 100L], tallies.Select(t => t.GetProperty("votes").GetInt64()));
        Assert.Equal([60.00m, 30.00m, 10.00m], tallies.Select(t => t.GetProperty("percentage").GetDecimal()));
        Assert.Equal("60.00", tallies[0].GetProperty("percentage").GetRawText());
        Assert.Equal("Eliminated", result.GetProperty("outcome").GetString());
        Assert.Equal(ParticipantId(poll, 0), result.GetProperty("eliminated").GetProperty("id").GetGuid());
        Assert.Equal("Ana", result.GetProperty("eliminated").GetProperty("name").GetString());
        Assert.Equal(1, result.GetProperty("eliminated").GetProperty("position").GetInt32());
        Assert.Empty(result.GetProperty("tiedParticipants").EnumerateArray());
    }

    [Fact]
    public async Task GetResult_400_400_200_ReturnsTieBetweenFirstTwo()
    {
        var poll = await CreateAndOpenPollAsync();
        await SendVotesAsync(poll, 400, 400, 200);

        var result = await CloseAndWaitForResultAsync(poll);

        Assert.Equal("Tie", result.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("eliminated").ValueKind);
        Assert.Equal(
            [ParticipantId(poll, 0), ParticipantId(poll, 1)],
            result.GetProperty("tiedParticipants").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task GetResult_NoVotes_ReturnsNoVotes()
    {
        var poll = await CreateAndOpenPollAsync();

        var result = await CloseAndWaitForResultAsync(poll);

        Assert.Equal("NoVotes", result.GetProperty("outcome").GetString());
        Assert.Equal(0, result.GetProperty("totalVotes").GetInt64());
        Assert.All(result.GetProperty("tallies").EnumerateArray(), t => Assert.Equal(0.00m, t.GetProperty("percentage").GetDecimal()));
    }

    [Fact]
    public async Task GetResult_AfterClosesAt_IsAvailableWithinFiveSecondsOfClock()
    {
        var poll = await CreateAndOpenPollAsync();
        await SendVotesAsync(poll, 3, 2, 1);

        var result = await CloseAndWaitForResultAsync(poll);

        var closesAt = poll.GetProperty("closesAt").GetDateTimeOffset();
        var finalizedAt = result.GetProperty("finalizedAt").GetDateTimeOffset();
        Assert.True(finalizedAt > closesAt);
        Assert.True(finalizedAt - closesAt <= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetResult_UnknownPoll_Returns404PollNotFound()
    {
        var response = await Client.GetAsync($"/polls/{Guid.NewGuid()}/result", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("poll_not_found", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task GetPoll_AfterFinalization_ReturnsFinalizedStatus()
    {
        var poll = await CreateAndOpenPollAsync();
        await CloseAndWaitForResultAsync(poll);

        var body = await Client.GetFromJsonAsync<JsonElement>($"/polls/{PollId(poll)}", Ct);

        Assert.Equal("Finalized", body.GetProperty("status").GetString());
    }

    private async Task SendVotesAsync(JsonElement poll, params int[] counts)
    {
        var votes = counts.SelectMany((count, index) => Enumerable.Range(0, count).Select(n => (index, key: $"p{index}-{n}"))).ToArray();
        foreach (var chunk in votes.Chunk(100))
        {
            var responses = await Task.WhenAll(chunk.Select(v => VoteAsync(PollId(poll), ParticipantId(poll, v.index), v.key)));
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        }
    }

    private async Task<JsonElement> CloseAndWaitForResultAsync(JsonElement poll)
    {
        Api.Time.SetUtcNow(poll.GetProperty("closesAt").GetDateTimeOffset().AddMilliseconds(300));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var response = await Client.GetAsync($"/polls/{PollId(poll)}/result", timeout.Token);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return await response.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
            }

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await Task.Delay(50, timeout.Token);
        }
    }
}
