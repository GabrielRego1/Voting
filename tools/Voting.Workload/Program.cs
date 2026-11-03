using Voting.Workload;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

return await WorkloadApp.RunAsync(args, Console.Out, Console.Error, cancellationToken: cancellation.Token);
