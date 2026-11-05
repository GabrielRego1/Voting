using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.WebApi.Fixtures;

namespace IntegrationTests.WebApi.Endpoints;

public sealed class PollEndpointsTests : ApiTest
{
    [Fact]
    public async Task CreatePoll_ThreeNames_Returns201WithLocationAndCreatedPoll()
    {
        var response = await Client.PostAsJsonAsync("/polls", new { participants = new[] { "Ana", "Bruno", "Carla" } }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var poll = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = poll.GetProperty("id").GetGuid();
        Assert.Equal($"/polls/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Created", poll.GetProperty("status").GetString());
        var participants = poll.GetProperty("participants").EnumerateArray().ToArray();
        Assert.Equal(["Ana", "Bruno", "Carla"], participants.Select(p => p.GetProperty("name").GetString()));
        Assert.Equal([1, 2, 3], participants.Select(p => p.GetProperty("position").GetInt32()));
    }

    public static TheoryData<string[]> InvalidParticipantLists => new()
    {
        { ["Ana", "Bruno"] },
        { ["Ana", new string('a', 101), "Carla"] },
        { ["Ana", "ana", "Carla"] },
    };

    [Theory]
    [MemberData(nameof(InvalidParticipantLists))]
    public async Task CreatePoll_InvalidParticipants_Returns400InvalidParticipants(string[] names)
    {
        var response = await Client.PostAsJsonAsync("/polls", new { participants = names }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_participants", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task GetPoll_ExistingPoll_Returns200()
    {
        var created = await CreatePollAsync();
        var id = created.GetProperty("id").GetGuid();

        var response = await Client.GetAsync($"/polls/{id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var poll = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(id, poll.GetProperty("id").GetGuid());
        Assert.Equal("Created", poll.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetPoll_UnknownPoll_Returns404PollNotFound()
    {
        var response = await Client.GetAsync($"/polls/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("poll_not_found", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task OpenPoll_CreatedPoll_Returns200WithClosesAtThirtySecondsAfterOpenedAt()
    {
        var created = await CreatePollAsync();

        var response = await Client.PostAsync($"/polls/{created.GetProperty("id").GetGuid()}/open", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var poll = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Open", poll.GetProperty("status").GetString());
        var openedAt = poll.GetProperty("openedAt").GetDateTimeOffset();
        var closesAt = poll.GetProperty("closesAt").GetDateTimeOffset();
        Assert.Equal(Api.Time.GetUtcNow(), openedAt);
        Assert.Equal(openedAt.AddSeconds(30), closesAt);
    }

    [Fact]
    public async Task OpenPoll_SecondCall_Returns409PollAlreadyOpened()
    {
        var opened = await CreateAndOpenPollAsync();

        var response = await Client.PostAsync($"/polls/{opened.GetProperty("id").GetGuid()}/open", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("poll_already_opened", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task OpenPoll_AnotherPollOpen_Returns409AnotherPollOpen()
    {
        await CreateAndOpenPollAsync();
        var second = await CreatePollAsync();

        var response = await Client.PostAsync($"/polls/{second.GetProperty("id").GetGuid()}/open", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("another_poll_open", await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task OpenPoll_UnknownPoll_Returns404PollNotFound()
    {
        var response = await Client.PostAsync($"/polls/{Guid.NewGuid()}/open", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("poll_not_found", await ReadProblemCodeAsync(response));
    }
}
