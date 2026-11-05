using Workload.Abstractions.ApiClient;

namespace Workload.ApiClient;

/// <summary>
/// Creates <see cref="VotingApiClient"/> instances. An injected <c>handler</c> (tests) is used as is; otherwise a
/// pooled <see cref="SocketsHttpHandler"/> limited to <c>connections</c> is created.
/// </summary>
internal sealed class VotingApiClientFactory(HttpMessageHandler? handler = null) : IVotingApiClientFactory
{
    public IVotingApiClient Create(Uri baseUrl, int connections) => new VotingApiClient(
        new HttpClient(
            handler ?? new SocketsHttpHandler
            {
                MaxConnectionsPerServer = connections,
                PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
            },
            disposeHandler: handler is null)
        {
            BaseAddress = baseUrl,
            Timeout = TimeSpan.FromSeconds(30),
        });
}
