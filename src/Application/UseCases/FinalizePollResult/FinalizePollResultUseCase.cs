using Application.Abstractions.Persistence;
using Domain.Services;

namespace Application.UseCases.FinalizePollResult;

/// <summary>
/// Tallies a closed poll once: drains the votes still queued, counts them and stores the result (research R-05).
/// </summary>
public sealed class FinalizePollResultUseCase(
    IPollRepository polls,
    IVoteWriter writer,
    IVoteTally tally,
    IResultRepository results,
    TimeProvider time)
{
    /// <summary>
    /// Returns <see langword="true"/> when this call stored the result.
    /// </summary>
    public async Task<bool> ExecuteAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        var poll = await polls.GetAsync(pollId, cancellationToken);
        if (poll?.ClosesAt is not { } closesAt || poll.IsFinalized || time.GetUtcNow() <= closesAt)
        {
            return false;
        }

        if (await results.GetAsync(pollId, cancellationToken) is not null)
        {
            return false;
        }

        await writer.FlushUntilAsync(closesAt, cancellationToken);
        var counts = await tally.CountByParticipantAsync(pollId, cancellationToken);
        var result = ResultCalculator.Calculate(poll, counts, time.GetUtcNow());
        return await results.TryAddAsync(result, cancellationToken);
    }
}
