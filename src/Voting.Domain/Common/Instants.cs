namespace Voting.Domain.Common;

public static class Instants
{
    /// <summary>
    /// Truncates an instant to microsecond precision, the precision every stored instant has,
    /// so that in-memory values always equal their persisted round-trip.
    /// </summary>
    public static DateTimeOffset Normalize(DateTimeOffset instant)
    {
        return instant.AddTicks(-(instant.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
