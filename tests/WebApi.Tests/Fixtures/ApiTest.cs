using System.Net.Http.Json;
using System.Text.Json;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Base class that gives each test its own <see cref="VotingApiFactory"/> (own host and database).
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTest : IAsyncLifetime
{
    protected VotingApiFactory Api { get; } = new();

    protected HttpClient Client { get; private set; } = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync()
    {
        await Api.InitializeAsync();
        Client = Api.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Api.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected async Task<JsonElement> CreatePollAsync(params string[] names)
    {
        var response = await Client.PostAsJsonAsync("/polls", new { participants = names.Length == 0 ? ["Ana", "Bruno", "Carla"] : names }, Ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    protected async Task<JsonElement> CreateAndOpenPollAsync()
    {
        var poll = await CreatePollAsync();
        var response = await Client.PostAsync($"/polls/{poll.GetProperty("id").GetGuid()}/open", null, Ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    protected Task<HttpResponseMessage> VoteAsync(Guid pollId, Guid participantId, string? idempotencyKey)
    {
        return VoteAsync(Client, pollId, participantId, idempotencyKey);
    }

    protected static async Task<HttpResponseMessage> VoteAsync(HttpClient client, Guid pollId, Guid participantId, string? idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/polls/{pollId}/votes")
        {
            Content = JsonContent.Create(new { participantId }),
        };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request, Ct);
    }

    protected static Guid ParticipantId(JsonElement poll, int index)
    {
        return poll.GetProperty("participants")[index].GetProperty("id").GetGuid();
    }

    protected static Guid PollId(JsonElement poll)
    {
        return poll.GetProperty("id").GetGuid();
    }

    protected static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("code").GetString();
    }
}
