using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Workload;

internal sealed record PollInfo(Guid Id, IReadOnlyList<Guid> ParticipantIds, DateTimeOffset? ClosesAt);

/// <summary>
/// HTTP client of the public voting API (contracts/voting-api.openapi.yaml), the same channel real voters use.
/// </summary>
internal sealed class VotingApiClient(HttpClient http) : IDisposable
{
    /// <summary>Total attempts per vote, including the first one.</summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Wait before the second attempt, doubled for the third (1 s, then 2 s). It honors <c>Retry-After: 1</c> and
    /// outlasts a quick API restart, so a vote committed just before a crash is confirmed by its retry.
    /// </summary>
    public static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(1);

    public HttpClient Http => http;

    /// <summary>
    /// Creates the client. An injected <c>handler</c> (tests) is used as is; otherwise a pooled
    /// <see cref="SocketsHttpHandler"/> limited to <c>connections</c> is created.
    /// </summary>
    public static VotingApiClient Create(Uri baseUrl, int connections, HttpMessageHandler? handler = null)
    {
        var client = handler is null
            ? new HttpClient(new SocketsHttpHandler
            {
                MaxConnectionsPerServer = connections,
                PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
            })
            : new HttpClient(handler, disposeHandler: false);
        client.BaseAddress = baseUrl;
        client.Timeout = TimeSpan.FromSeconds(30);
        return new VotingApiClient(client);
    }

    public async Task<PollInfo> CreatePollAsync(IReadOnlyList<string> participants, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("/polls", new { participants }, cancellationToken);
        return await ReadPollAsync(response, cancellationToken);
    }

    public async Task<PollInfo> OpenPollAsync(Guid pollId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(new Uri($"/polls/{pollId}/open", UriKind.Relative), null, cancellationToken);
        return await ReadPollAsync(response, cancellationToken);
    }

    public async Task<PollInfo> GetPollAsync(Guid pollId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"/polls/{pollId}", UriKind.Relative), cancellationToken);
        return await ReadPollAsync(response, cancellationToken);
    }

    /// <summary>
    /// Sends a vote, retrying with the same key on <c>503</c> or transport errors up to <see cref="MaxAttempts"/>.
    /// Every attempt's latency is recorded.
    /// </summary>
    public async Task<VoteResponse> CastVoteAsync(
        Guid pollId,
        Guid participantId,
        string idempotencyKey,
        LatencyRecorder latency,
        Action onRetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(latency);
        ArgumentNullException.ThrowIfNull(onRetry);
        var uri = new Uri($"/polls/{pollId}/votes", UriKind.Relative);
        var body = $$"""{"participantId":"{{participantId}}"}""";
        var response = VoteResponse.TransportError;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                onRetry();
                await Task.Delay(RetryBaseDelay * Math.Pow(2, attempt - 2), cancellationToken);
            }

            response = await SendVoteAsync(uri, body, idempotencyKey, latency, cancellationToken);
            if (response.StatusCode is not null and not (int)HttpStatusCode.ServiceUnavailable)
            {
                return response;
            }
        }

        return response;
    }

    /// <summary>
    /// Returns the result body once available, or <see langword="null"/> while the API answers <c>409</c>.
    /// </summary>
    public async Task<JsonElement?> GetResultAsync(Guid pollId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"/polls/{pollId}/result", UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public void Dispose()
    {
        http.Dispose();
    }

    private async Task<VoteResponse> SendVoteAsync(
        Uri uri,
        string body,
        string idempotencyKey,
        LatencyRecorder latency,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            string? code = null;
            if (!response.IsSuccessStatusCode)
            {
                code = await ReadProblemCodeAsync(response, cancellationToken);
            }

            latency.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return new VoteResponse((int)response.StatusCode, code);
        }
        catch (HttpRequestException)
        {
            latency.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return VoteResponse.TransportError;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient timeout.
            latency.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return VoteResponse.TransportError;
        }
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("code", out var code) ? code.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<PollInfo> ReadPollAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"API answered {(int)response.StatusCode}: {detail}", null, response.StatusCode);
        }

        var poll = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var participants = poll.GetProperty("participants").EnumerateArray()
            .OrderBy(p => p.GetProperty("position").GetInt32())
            .Select(p => p.GetProperty("id").GetGuid())
            .ToArray();
        var closesAt = poll.TryGetProperty("closesAt", out var c) && c.ValueKind == JsonValueKind.String ? c.GetDateTimeOffset() : (DateTimeOffset?)null;
        return new PollInfo(poll.GetProperty("id").GetGuid(), participants, closesAt);
    }
}
