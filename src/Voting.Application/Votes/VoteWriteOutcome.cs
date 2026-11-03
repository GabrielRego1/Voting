namespace Voting.Application.Votes;

public enum VoteWriteStatus
{
    /// <summary>The vote was committed for the first time.</summary>
    Accepted,

    /// <summary>The key was already committed with the same participant; the original receipt is returned.</summary>
    Replayed,

    /// <summary>The key was already committed with a different participant.</summary>
    Conflict,

    /// <summary>The write queue is full; nothing was recorded.</summary>
    Overloaded,
}

public sealed record VoteWriteOutcome(VoteWriteStatus Status, VoteReceipt? Receipt)
{
    public static VoteWriteOutcome Conflict { get; } = new(VoteWriteStatus.Conflict, null);

    public static VoteWriteOutcome Overloaded { get; } = new(VoteWriteStatus.Overloaded, null);

    public static VoteWriteOutcome Accepted(VoteReceipt receipt)
    {
        return new VoteWriteOutcome(VoteWriteStatus.Accepted, receipt);
    }

    public static VoteWriteOutcome Replayed(VoteReceipt receipt)
    {
        return new VoteWriteOutcome(VoteWriteStatus.Replayed, receipt);
    }
}
