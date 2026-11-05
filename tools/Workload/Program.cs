using Microsoft.Extensions.DependencyInjection;
using Workload;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

await using var services = new ServiceCollection().AddWorkload().BuildServiceProvider();
var app = services.GetRequiredService<IWorkloadApp>();
return await app.RunAsync(args, Console.Out, Console.Error, cancellationToken: cancellation.Token);
