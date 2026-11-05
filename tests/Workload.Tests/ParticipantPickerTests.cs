namespace Workload.Tests;

public sealed class ParticipantPickerTests
{
    [Fact]
    public void Pick_SeededRandom100000Draws_FollowsDistributionWithinOnePoint()
    {
        var picker = new ParticipantPicker([50, 30, 20], new Random(42));
        var counts = new int[3];

        for (var i = 0; i < 100_000; i++)
        {
            counts[picker.Pick()]++;
        }

        Assert.InRange(counts[0] / 1000.0, 49, 51);
        Assert.InRange(counts[1] / 1000.0, 29, 31);
        Assert.InRange(counts[2] / 1000.0, 19, 21);
    }

    [Fact]
    public void Pick_ZeroWeight_NeverPicksThatParticipant()
    {
        var picker = new ParticipantPicker([50, 50, 0], new Random(7));

        var picks = Enumerable.Range(0, 10_000).Select(_ => picker.Pick()).ToArray();

        Assert.DoesNotContain(2, picks);
    }
}
