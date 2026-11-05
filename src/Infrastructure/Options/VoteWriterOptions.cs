using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Options;

/// <summary>
/// Tuning of the group-commit vote writer, bound to the <c>VoteWriter</c> configuration section.
/// </summary>
public sealed class VoteWriterOptions
{
    public const string ConfigurationPath = "VoteWriter";

    /// <summary>Maximum number of votes committed in a single transaction.</summary>
    [Range(1, int.MaxValue)]
    public int MaxBatchSize { get; init; } = 1000;

    /// <summary>Maximum real time the oldest queued vote waits for its batch to fill.</summary>
    [Range(typeof(TimeSpan), "00:00:00.001", "00:01:00")]
    public TimeSpan MaxBatchDelay { get; init; } = TimeSpan.FromMilliseconds(5);

    /// <summary>Maximum number of queued votes; beyond it votes are refused as overloaded.</summary>
    [Range(1, int.MaxValue)]
    public int Capacity { get; init; } = 50000;
}
