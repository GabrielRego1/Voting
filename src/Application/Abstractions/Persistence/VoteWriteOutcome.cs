using Application.Dtos;

namespace Application.Abstractions.Persistence;

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
