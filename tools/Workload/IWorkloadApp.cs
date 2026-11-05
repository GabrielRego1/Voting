namespace Workload;

/// <summary>
/// Runs the load generator: prepares the poll, sends the load, waits for the result and reports.
/// </summary>
internal interface IWorkloadApp
{
    /// <summary>
    /// Runs one execution and returns the exit code of contracts/workload-cli.md. <c>afterLoad</c> runs after the
    /// load and before waiting for the result (integration tests use it to close the poll).
    /// </summary>
    Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        Func<HttpClient, Guid, Task>? afterLoad = null,
        CancellationToken cancellationToken = default);
}
