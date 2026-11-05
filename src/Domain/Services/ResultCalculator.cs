using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

namespace Domain.Services;

/// <summary>
/// Tallies a poll. Percentages use the largest remainder method in hundredths of a percent, so they
/// always sum to exactly 100.00 when there are votes. The outcome is decided by exact counts, never by
/// rounded percentages (research R-06).
/// </summary>
public sealed class ResultCalculator : IResultCalculator
{
    private const long HundredPercentInHundredths = 10_000;

    public PollResult Calculate(Poll poll, IReadOnlyDictionary<Guid, long> votesByParticipant, DateTimeOffset finalizedAt)
    {
        ArgumentNullException.ThrowIfNull(poll);
        ArgumentNullException.ThrowIfNull(votesByParticipant);

        var participants = poll.Participants;
        var counts = participants.Select(p => votesByParticipant.GetValueOrDefault(p.Id)).ToArray();
        var total = counts.Sum();
        var hundredths = Apportion(counts, total);

        var tallies = participants
            .Select((p, i) => new ParticipantTally(p.Id, p.Name, counts[i], new decimal(hundredths[i], 0, 0, false, 2)))
            .ToArray();

        if (total == 0)
        {
            return new PollResult(poll.Id, 0, tallies, Outcome.NoVotes, null, [], Instants.Normalize(finalizedAt));
        }

        var max = counts.Max();
        var leaders = participants.Where((_, i) => counts[i] == max).Select(p => p.Id).ToArray();
        return leaders.Length == 1
            ? new PollResult(poll.Id, total, tallies, Outcome.Eliminated, leaders[0], [], Instants.Normalize(finalizedAt))
            : new PollResult(poll.Id, total, tallies, Outcome.Tie, null, leaders, Instants.Normalize(finalizedAt));
    }

    private static int[] Apportion(long[] counts, long total)
    {
        var hundredths = new int[counts.Length];
        if (total == 0)
        {
            return hundredths;
        }

        var remainders = new long[counts.Length];
        long assigned = 0;
        for (var i = 0; i < counts.Length; i++)
        {
            var scaled = counts[i] * HundredPercentInHundredths;
            hundredths[i] = (int)(scaled / total);
            remainders[i] = scaled % total;
            assigned += hundredths[i];
        }

        // Ties between equal remainders go to the lower position, keeping the result deterministic.
        var byRemainder = Enumerable.Range(0, counts.Length)
            .OrderByDescending(i => remainders[i])
            .ThenBy(i => i)
            .ToArray();
        for (var k = 0; k < HundredPercentInHundredths - assigned; k++)
        {
            hundredths[byRemainder[k]]++;
        }

        return hundredths;
    }
}
