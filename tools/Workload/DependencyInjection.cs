using Microsoft.Extensions.DependencyInjection;
using Workload.Abstractions;
using Workload.Abstractions.ApiClient;
using Workload.Abstractions.Configuration;
using Workload.Abstractions.Load;
using Workload.Abstractions.Metrics;
using Workload.Abstractions.Reporting;
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
    public static IServiceCollection AddWorkload(this IServiceCollection services, HttpMessageHandler? handler = null) => services
        .AddSingleton<IOptionsParser, OptionsParser>()
        .AddSingleton<IVotingApiClientFactory>(_ => new VotingApiClientFactory(handler))
        .AddSingleton<ILoadRunner, LoadRunner>()
        .AddSingleton<IProgressReporter>(_ => new ProgressReporter(ProgressReporter.DefaultInterval))
        .AddSingleton<IReportWriter, ReportWriter>()
        .AddSingleton<IWorkloadApp, WorkloadApp>();
}
