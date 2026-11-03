using Voting.Domain.Polls;
using Voting.Domain.Results;

namespace Voting.Domain.Tests.Results;

public sealed class ResultCalculatorTests
{
    private static readonly DateTimeOffset FinalizedAt = new(2026, 11, 2, 12, 0, 31, TimeSpan.Zero);

    private static readonly Poll Poll = Poll.Create(["Ana", "Bruno", "Carla"], FinalizedAt.AddMinutes(-1)).Value;

    [Fact]
    public void Calculate_600_300_100_EliminatesFirstWithExactPercentages()
    {
        var result = Calculate(600, 300, 100);

        Assert.Equal(1000, result.TotalVotes);
        Assert.Equal([60.00m, 30.00m, 10.00m], result.Tallies.Select(t => t.Percentage));
        Assert.Equal(Outcome.Eliminated, result.Outcome);
        Assert.Equal(Poll.Participants[0].Id, result.EliminatedParticipantId);
        Assert.Empty(result.TiedParticipantIds);
        Assert.Equal(FinalizedAt, result.FinalizedAt);
        Assert.Equal(Poll.Id, result.PollId);
        Assert.Equal(["Ana", "Bruno", "Carla"], result.Tallies.Select(t => t.Name));
    }

    [Fact]
    public void Calculate_400_400_200_IsTieBetweenFirstTwo()
    {
        var result = Calculate(400, 400, 200);

        Assert.Equal(Outcome.Tie, result.Outcome);
        Assert.Null(result.EliminatedParticipantId);
        Assert.Equal([Poll.Participants[0].Id, Poll.Participants[1].Id], result.TiedParticipantIds);
    }

    [Fact]
    public void Calculate_1_1_1_IsTieBetweenAllWithPercentagesSummingTo100()
    {
        var result = Calculate(1, 1, 1);

        Assert.Equal(Outcome.Tie, result.Outcome);
        Assert.Equal(3, result.TiedParticipantIds.Count);
        Assert.Equal([33.34m, 33.33m, 33.33m], result.Tallies.Select(t => t.Percentage));
        Assert.Equal(100.00m, result.Tallies.Sum(t => t.Percentage));
    }

    [Fact]
    public void Calculate_NoVotes_ReturnsNoVotesWithZeroPercentages()
    {
        var result = Calculate(0, 0, 0);

        Assert.Equal(Outcome.NoVotes, result.Outcome);
        Assert.Equal(0, result.TotalVotes);
        Assert.Null(result.EliminatedParticipantId);
        Assert.Empty(result.TiedParticipantIds);
        Assert.All(result.Tallies, t => Assert.Equal(0.00m, t.Percentage));
        Assert.All(result.Tallies, t => Assert.Equal(0, t.Votes));
    }

    [Fact]
    public void Calculate_ParticipantWithoutVotes_AppearsWithZeroPercent()
    {
        var result = Calculate(2, 1, 0);

        Assert.Equal(3, result.Tallies.Count);
        Assert.Equal(0, result.Tallies[2].Votes);
        Assert.Equal(0.00m, result.Tallies[2].Percentage);
        Assert.Equal(100.00m, result.Tallies.Sum(t => t.Percentage));
    }

    [Fact]
    public void Calculate_RoundedPercentagesTieButCountsDiffer_DecidesByExactCount()
    {
        var result = Calculate(10001, 10000, 0);

        Assert.Equal(result.Tallies[0].Percentage, result.Tallies[1].Percentage);
        Assert.Equal(Outcome.Eliminated, result.Outcome);
        Assert.Equal(Poll.Participants[0].Id, result.EliminatedParticipantId);
    }

    [Fact]
    public void Calculate_PercentagesHaveTwoDecimalPlaces()
    {
        var result = Calculate(600, 300, 100);

        Assert.All(result.Tallies, t => Assert.Equal(2, t.Percentage.Scale));
    }

    [Fact]
    public void Calculate_200RandomDistributions_PercentagesAlwaysSumTo100()
    {
        var random = new Random(20261102);
        for (var i = 0; i < 200; i++)
        {
            var counts = new long[] { random.Next(0, 100_000), random.Next(0, 100_000), random.Next(1, 100_000) };

            var result = Calculate(counts[0], counts[1], counts[2]);

            Assert.Equal(100.00m, result.Tallies.Sum(t => t.Percentage));
        }
    }

    private static PollResult Calculate(long first, long second, long third)
    {
        var votes = new Dictionary<Guid, long>
        {
            [Poll.Participants[0].Id] = first,
            [Poll.Participants[1].Id] = second,
            [Poll.Participants[2].Id] = third,
        };
        return ResultCalculator.Calculate(Poll, votes, FinalizedAt);
    }
}
