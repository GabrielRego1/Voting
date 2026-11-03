using Voting.Domain.Common;

namespace Voting.Domain.Polls;

/// <summary>
/// Reasons why the poll refuses a vote.
/// </summary>
public static class VoteRejection
{
    public static readonly DomainError InvalidParticipant = new(
        "invalid_participant",
        "The participant does not belong to this poll.");
}
