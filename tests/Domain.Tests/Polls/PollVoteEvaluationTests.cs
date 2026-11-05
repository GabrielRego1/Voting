using Domain.Entities;
using Domain.Enums;

namespace Domain.Tests.Polls;

public sealed class PollVoteEvaluationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EvaluateVote_KnownParticipantWithinWindow_IsAccepted()
    {
        var poll = OpenPoll();

        var evaluation = poll.EvaluateVote(poll.Participants[1].Id, Now.AddSeconds(10));

        Assert.True(evaluation.IsAccepted);
        Assert.Null(evaluation.Rejection);
    }

    [Fact]
    public void EvaluateVote_UnknownParticipant_IsRejectedWithInvalidParticipant()
    {
        var poll = OpenPoll();

        var evaluation = poll.EvaluateVote(Guid.NewGuid(), Now.AddSeconds(10));

        Assert.False(evaluation.IsAccepted);
        Assert.Equal("invalid_participant", evaluation.Rejection!.Code);
    }

    [Fact]
    public void EvaluateVote_SameParticipantFiveTimes_AllAccepted()
    {
        var poll = OpenPoll();
        var participantId = poll.Participants[0].Id;

        var evaluations = Enumerable.Range(1, 5)
            .Select(i => poll.EvaluateVote(participantId, Now.AddSeconds(i)))
            .ToArray();

        Assert.All(evaluations, e => Assert.True(e.IsAccepted));
    }

    [Fact]
    public void EvaluateVote_ReceivedExactlyAtClosesAt_IsAccepted()
    {
        var poll = OpenPoll();

        var evaluation = poll.EvaluateVote(poll.Participants[0].Id, poll.ClosesAt!.Value);

        Assert.True(evaluation.IsAccepted);
    }

    [Fact]
    public void EvaluateVote_ReceivedOneTickAfterClosesAt_IsRejectedWithVotingClosed()
    {
        var poll = OpenPoll();

        var evaluation = poll.EvaluateVote(poll.Participants[0].Id, poll.ClosesAt!.Value.AddTicks(1));

        Assert.Equal("voting_closed", evaluation.Rejection!.Code);
    }

    [Fact]
    public void EvaluateVote_PollNotOpened_IsRejectedWithVotingNotOpen()
    {
        var poll = Poll.Create(["Ana", "Bruno", "Carla"], Now).Value;

        var evaluation = poll.EvaluateVote(poll.Participants[0].Id, Now.AddSeconds(1));

        Assert.Equal("voting_not_open", evaluation.Rejection!.Code);
    }

    [Fact]
    public void EvaluateVote_ReceivedBeforeOpenedAt_IsRejectedWithVotingNotOpen()
    {
        var poll = OpenPoll();

        var evaluation = poll.EvaluateVote(poll.Participants[0].Id, Now.AddTicks(-1));

        Assert.Equal("voting_not_open", evaluation.Rejection!.Code);
    }

    [Fact]
    public void StatusAt_OneTickAfterClosesAt_ReturnsClosed()
    {
        var poll = OpenPoll();

        Assert.Equal(PollStatus.Closed, poll.StatusAt(poll.ClosesAt!.Value.AddTicks(1)));
    }

    private static Poll OpenPoll()
    {
        var poll = Poll.Create(["Ana", "Bruno", "Carla"], Now).Value;
        poll.Open(Now);
        return poll;
    }
}
