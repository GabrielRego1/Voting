using Domain.Votes;

namespace Application.Votes;

public sealed record VoteReceipt(Guid VoteId, Guid PollId, Guid ParticipantId, DateTimeOffset ReceivedAt)
{
    public static VoteReceipt From(Vote vote)
    {
        ArgumentNullException.ThrowIfNull(vote);
        return new VoteReceipt(vote.Id, vote.PollId, vote.ParticipantId, vote.ReceivedAt);
    }
}
