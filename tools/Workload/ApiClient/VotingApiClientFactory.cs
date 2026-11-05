namespace Workload.ApiClient;

/// <summary>
/// Creates <see cref="VotingApiClient"/> instances. An injected <c>handler</c> (tests) is used as is; otherwise a
/// pooled <see cref="SocketsHttpHandler"/> limited to <c>connections</c> is created.
/// </summary>
internal sealed class VotingApiClientFactory(HttpMessageHandler? handler = null) : IVotingApiClientFactory
{
    public IVotingApiClient Create(Uri baseUrl, int connections)
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
}
