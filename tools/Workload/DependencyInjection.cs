using Microsoft.Extensions.DependencyInjection;
using Workload.ApiClient;
using Workload.Configuration;
using Workload.Load;
using Workload.Metrics;
using Workload.Reporting;

namespace Workload;

internal static class DependencyInjection
{
    /// <summary>
    /// Registers the load generator services. An injected <paramref name="handler"/> lets integration tests reach
    /// an in-memory API instead of the network.
    /// </summary>
    public static IServiceCollection AddWorkload(this IServiceCollection services, HttpMessageHandler? handler = null)
    {
        services.AddSingleton<IOptionsParser, OptionsParser>();
        services.AddSingleton<IVotingApiClientFactory>(_ => new VotingApiClientFactory(handler));
        services.AddSingleton<ILoadRunner, LoadRunner>();
        services.AddSingleton<IProgressReporter>(_ => new ProgressReporter(ProgressReporter.DefaultInterval));
        services.AddSingleton<IReportWriter, ReportWriter>();
        services.AddSingleton<IWorkloadApp, WorkloadApp>();
        return services;
    }
}
