using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Tests.Fixtures;

namespace Workload.Tests;

public sealed class WorkloadIntegrationTests : IAsyncLifetime
{
    private readonly VotingApiFactory _api = new();

    public async ValueTask InitializeAsync()
    {
        await _api.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task RunAsync_LowRateAgainstApi_AcceptedMatchesResultWithoutDuplicates()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var handler = _api.Server.CreateHandler();
        await using var services = new ServiceCollection().AddWorkload(handler).BuildServiceProvider();

        var exitCode = await services.GetRequiredService<IWorkloadApp>().RunAsync(
            ["--base-url", "http://localhost", "--rate", "100", "--duration", "3", "--retry-fraction", "0.1", "--connections", "16", "--output", "json"],
            output,
            error,
            afterLoad: async (client, pollId) =>
            {
                var poll = await client.GetFromJsonAsync<JsonElement>($"/polls/{pollId}");
                _api.Time.SetUtcNow(poll.GetProperty("closesAt").GetDateTimeOffset().AddSeconds(1));
            },
            cancellationToken: TestContext.Current.CancellationToken);

        using var report = JsonDocument.Parse(output.ToString());
        var root = report.RootElement;
        var counts = root.GetProperty("counts");
        Assert.True(root.GetProperty("checks").GetProperty("acceptedEqualsTotalVotes").GetBoolean(), output.ToString());
        Assert.True(root.GetProperty("checks").GetProperty("noDuplicatesFromReplays").GetBoolean(), output.ToString());
        Assert.InRange(counts.GetProperty("accepted").GetInt64(), 200, 400);
        Assert.True(counts.GetProperty("replayed").GetInt64() > 0, output.ToString());
        Assert.Equal(counts.GetProperty("accepted").GetInt64(), root.GetProperty("result").GetProperty("totalVotes").GetInt64());
        Assert.Contains("sending 100 votes/s", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("waiting for the result", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, exitCode);
    }
}
