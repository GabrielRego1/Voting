using System.Net.Http.Json;
using System.Text.Json;

namespace Voting.Api.Tests.Fixtures;

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

    protected static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("code").GetString();
    }
}
