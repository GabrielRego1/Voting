using Voting.Domain.Common;

namespace Voting.Domain.Polls;

/// <summary>
/// Whether a vote is accepted by the poll, or the <see cref="Rejection"/> reason otherwise.
/// </summary>
public readonly record struct VoteEvaluation(DomainError? Rejection)
{
    public static VoteEvaluation Accepted { get; } = new(null);

    public bool IsAccepted => Rejection is null;

    public static VoteEvaluation Rejected(DomainError rejection)
    {
        return new VoteEvaluation(rejection);
    }
}
