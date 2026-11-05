namespace Workload.Load;

/// <summary>
/// Draws a participant index (0-based, in position order) following the percentage distribution.
/// Not thread-safe: used only by the scheduling loop.
/// </summary>
internal sealed class ParticipantPicker(IReadOnlyList<int> distribution, Random random)
{
    private readonly int[] _cumulative = Cumulative(distribution);

    public int Pick()
    {
        var draw = random.Next(_cumulative[^1]);
        for (var i = 0; i < _cumulative.Length; i++)
        {
            if (draw < _cumulative[i])
            {
                return i;
            }
        }

        return _cumulative.Length - 1;
    }

    private static int[] Cumulative(IReadOnlyList<int> distribution)
    {
        var cumulative = new int[distribution.Count];
        var sum = 0;
        for (var i = 0; i < distribution.Count; i++)
        {
            sum += distribution[i];
            cumulative[i] = sum;
        }

        return cumulative;
    }
}
