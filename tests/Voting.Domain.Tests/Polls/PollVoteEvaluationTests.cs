using Voting.Domain.Polls;

namespace Voting.Domain.Tests.Polls;

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

    private static Poll OpenPoll()
    {
        var poll = Poll.Create(["Ana", "Bruno", "Carla"], Now).Value;
        poll.Open(Now);
        return poll;
    }
}
