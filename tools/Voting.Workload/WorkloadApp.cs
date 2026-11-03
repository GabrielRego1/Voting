using System.Diagnostics;
using System.Text.Json;

namespace Voting.Workload;

/// <summary>
/// Runs the load generator: prepares the poll, sends the load, waits for the result and reports.
/// Exit codes follow contracts/workload-cli.md (0 = all checks passed, 1 = a check failed, 2 = usage or API error).
/// </summary>
internal static class WorkloadApp
{
    public const int UsageOrApiError = 2;

    /// <summary>
    /// Runs one execution. <c>handler</c> lets integration tests reach an in-memory API, and <c>afterLoad</c>
    /// runs after the load and before waiting for the result.
    /// </summary>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        HttpMessageHandler? handler = null,
        Func<HttpClient, Guid, Task>? afterLoad = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        var parsed = OptionsParser.Parse(args);
        if (parsed.Options is not { } options)
        {
            await error.WriteLineAsync(parsed.Error);
            await error.WriteLineAsync(OptionsParser.Usage);
            return UsageOrApiError;
        }

        using var client = VotingApiClient.Create(options.BaseUrl, options.Connections, handler);
        PollInfo poll;
        try
        {
            poll = options.PollId is { } pollId
                ? await client.GetPollAsync(pollId, cancellationToken)
                : await client.OpenPollAsync((await client.CreatePollAsync(options.Participants, cancellationToken)).Id, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            await error.WriteLineAsync($"Could not prepare the poll at {options.BaseUrl}: {ex.Message}");
            return UsageOrApiError;
        }

        var report = new ReportBuilder();
        var latency = new LatencyRecorder();
        var sendingTime = await new LoadRunner(client, options, report, latency, new Random())
            .RunAsync(poll, cancellationToken);

        if (afterLoad is not null)
        {
            await afterLoad(client.Http, poll.Id);
        }

        var result = await WaitForResultAsync(client, poll.Id, TimeSpan.FromSeconds(options.ResultTimeoutSeconds), error, cancellationToken);
        var parameters = new RunParameters(options.Rate, options.DurationSeconds, options.Distribution, options.RetryFraction, options.Connections);
        var final = report.Build(poll.Id, parameters, sendingTime, latency.Summarize(), result);
        ReportWriter.Write(final, options.Output, output);
        return final.ExitCode;
    }

    private static async Task<JsonElement?> WaitForResultAsync(
        VotingApiClient client,
        Guid pollId,
        TimeSpan timeout,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                if (await client.GetResultAsync(pollId, cancellationToken) is { } result)
                {
                    return result;
                }
            }
            catch (HttpRequestException ex)
            {
                await error.WriteLineAsync($"Failed to read the result: {ex.Message}");
            }

            if (stopwatch.Elapsed >= timeout)
            {
                await error.WriteLineAsync($"The result was not available within {timeout.TotalSeconds:0} s.");
                return null;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }
}
