using Application.Abstractions.Persistence;
using Application.UseCases.FinalizePollResult;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Hosting;

/// <summary>
/// Finalizes polls whose window has closed. Runs once on startup (recovering polls that closed while the
/// API was down, FR-020) and then every <see cref="Interval"/> of the business clock.
/// </summary>
public sealed partial class PollFinalizer(
    IPollRepository polls,
    FinalizePollResultUseCase finalizePollResult,
    TimeProvider time,
    ILogger<PollFinalizer> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Created before the first await so a clock advanced right after startup is never missed.
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            await FinalizeDuePollsAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task FinalizeDuePollsAsync(CancellationToken stoppingToken)
    {
        try
        {
            foreach (var poll in await polls.ListDueForFinalizationAsync(time.GetUtcNow(), stoppingToken))
            {
                if (await finalizePollResult.ExecuteAsync(poll.Id, stoppingToken))
                {
                    LogFinalized(logger, poll.Id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; the next startup recovers any poll left unfinalized.
        }
#pragma warning disable CA1031 // A failed pass is logged and retried on the next tick instead of stopping the host.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Poll {PollId} finalized.")]
    private static partial void LogFinalized(ILogger logger, Guid pollId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Poll finalization pass failed; retrying on the next tick.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
