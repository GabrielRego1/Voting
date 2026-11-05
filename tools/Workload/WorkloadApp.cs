using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Workload.ApiClient;
using Workload.Configuration;
using Workload.Load;
using Workload.Metrics;
using Workload.Reporting;

namespace Workload;

/// <summary>
/// Runs the load generator: prepares the poll, sends the load, waits for the result and reports.
/// Exit codes follow contracts/workload-cli.md (0 = all checks passed, 1 = a check failed, 2 = usage or API error).
/// </summary>
internal sealed class WorkloadApp(
    IOptionsParser parser,
    IVotingApiClientFactory clients,
    ILoadRunner loadRunner,
    IProgressReporter progressReporter,
    IReportWriter reportWriter) : IWorkloadApp
{
    private const int UsageOrApiError = 2;

    public async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        Func<HttpClient, Guid, Task>? afterLoad = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        var parsed = parser.Parse(args);
        if (parsed.Options is not { } options)
        {
            await error.WriteLineAsync(parsed.Error);
            await error.WriteLineAsync(parser.Usage);
            return UsageOrApiError;
        }

        using var client = clients.Create(options.BaseUrl, options.Connections);
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
        await error.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"Poll {poll.Id} open; sending {options.Rate} votes/s for {options.DurationSeconds} s..."));

        TimeSpan sendingTime;
        using (var progressCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var progress = progressReporter.RunAsync(
                error,
                report,
                TimeSpan.FromSeconds(options.DurationSeconds),
                progressCancellation.Token);
            try
            {
                sendingTime = await loadRunner.RunAsync(client, poll, options, report, latency, cancellationToken);
            }
            finally
            {
                // Stop the progress lines before anything else writes to the error stream.
                await progressCancellation.CancelAsync();
                await progress;
            }
        }

        await error.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"Load finished in {sendingTime.TotalSeconds:0.0} s; waiting for the result (up to {options.ResultTimeoutSeconds} s)..."));

        if (afterLoad is not null)
        {
            await afterLoad(client.Http, poll.Id);
        }

        var result = await WaitForResultAsync(client, poll.Id, TimeSpan.FromSeconds(options.ResultTimeoutSeconds), error, cancellationToken);
        var parameters = new RunParameters(options.Rate, options.DurationSeconds, options.Distribution, options.RetryFraction, options.Connections);
        var final = report.Build(poll.Id, parameters, sendingTime, latency.Summarize(), result);
        reportWriter.Write(final, options.Output, output);
        return final.ExitCode;
    }

    private static async Task<JsonElement?> WaitForResultAsync(
        IVotingApiClient client,
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
