using Domain.Common;

namespace Domain.Errors;

/// <summary>
/// Reasons why the poll refuses a vote.
/// </summary>
public static class VoteRejection
{
    public static readonly DomainError InvalidParticipant = new(
        "invalid_participant",
        "The participant does not belong to this poll.");

    public static readonly DomainError VotingNotOpen = new(
        "voting_not_open",
        "Voting has not been opened for this poll yet.");

    public static readonly DomainError VotingClosed = new(
        "voting_closed",
        "Voting for this poll has closed.");
}
