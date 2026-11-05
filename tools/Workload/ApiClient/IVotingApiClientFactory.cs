namespace Workload.ApiClient;

/// <summary>
/// Creates API clients for a run, whose address and connection limit come from the CLI options.
/// </summary>
internal interface IVotingApiClientFactory
{
    IVotingApiClient Create(Uri baseUrl, int connections);
}
