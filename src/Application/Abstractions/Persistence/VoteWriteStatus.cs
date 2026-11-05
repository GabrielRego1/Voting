namespace Application.Abstractions.Persistence;

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
