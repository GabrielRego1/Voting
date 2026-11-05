using Microsoft.Extensions.DependencyInjection;

namespace Workload;

/// <summary>
/// Entry point. It is a named class (not top-level statements) so it does not clash with the API's global
/// <c>Program</c> in test assemblies that reference both projects.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        await using var services = new ServiceCollection().AddWorkload().BuildServiceProvider();
        var app = services.GetRequiredService<IWorkloadApp>();
        return await app.RunAsync(args, Console.Out, Console.Error, cancellationToken: cancellation.Token);
    }
}
