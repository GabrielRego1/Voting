namespace Infrastructure.Configuration;

/// <summary>
/// Tuning of the group-commit vote writer, bound to the <c>VoteWriter</c> configuration section.
/// </summary>
public sealed class VoteWriterOptions
{
    public const string SectionName = "VoteWriter";

    /// <summary>Maximum number of votes committed in a single transaction.</summary>
    public int MaxBatchSize { get; set; } = 1000;

    /// <summary>Maximum real time the oldest queued vote waits for its batch to fill.</summary>
    public TimeSpan MaxBatchDelay { get; set; } = TimeSpan.FromMilliseconds(5);

    /// <summary>Maximum number of queued votes; beyond it votes are refused as overloaded.</summary>
    public int Capacity { get; set; } = 50000;
}
