using Domain.Entities;
using Domain.Enums;

namespace UnitTests.Domain.Entities;

public sealed class PollTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ThreeDistinctNames_ReturnsCreatedPollWithPositionsInOrder()
    {
        var result = Poll.Create(["Ana", "Bruno", "Carla"], Now);

        Assert.True(result.IsSuccess);
        var poll = result.Value;
        Assert.NotEqual(Guid.Empty, poll.Id);
        Assert.Equal(7, poll.Id.Version);
        Assert.Equal(Now, poll.CreatedAt);
        Assert.Null(poll.OpenedAt);
        Assert.Null(poll.ClosesAt);
        Assert.Equal(["Ana", "Bruno", "Carla"], poll.Participants.Select(p => p.Name));
        Assert.Equal([1, 2, 3], poll.Participants.Select(p => p.Position));
        Assert.Equal(3, poll.Participants.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Create_NamesWithSurroundingSpaces_TrimsNames()
    {
        var result = Poll.Create(["  Ana ", "Bruno", "Carla  "], Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Ana", "Bruno", "Carla"], result.Value.Participants.Select(p => p.Name));
    }

    [Fact]
    public void Create_NameWith100Characters_IsAccepted()
    {
        var result = Poll.Create([new string('a', 100), "Bruno", "Carla"], Now);

        Assert.True(result.IsSuccess);
    }

    public static TheoryData<string[]> InvalidParticipantLists => new()
    {
        { ["Ana", "Bruno"] },
        { ["Ana", "Bruno", "Carla", "Duda"] },
        { ["Ana", "", "Carla"] },
        { ["Ana", "   ", "Carla"] },
        { ["Ana", new string('a', 101), "Carla"] },
        { ["Ana", " ana ", "Carla"] },
        { [] },
    };

    [Theory]
    [MemberData(nameof(InvalidParticipantLists))]
    public void Create_InvalidParticipants_FailsWithInvalidParticipants(string[] names)
    {
        var result = Poll.Create(names, Now);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_participants", result.Error!.Code);
    }

    [Fact]
    public void Open_CreatedPoll_SetsOpenedAtAndClosesAtThirtySecondsLater()
    {
        var poll = CreatePoll();

        var result = poll.Open(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now, poll.OpenedAt);
        Assert.Equal(Now.AddSeconds(30), poll.ClosesAt);
        Assert.Equal(TimeSpan.FromSeconds(30), Poll.Duration);
    }

    [Fact]
    public void Open_AlreadyOpenedPoll_FailsWithPollAlreadyOpened()
    {
        var poll = CreatePoll();
        poll.Open(Now);

        var result = poll.Open(Now.AddSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.Equal("poll_already_opened", result.Error!.Code);
        Assert.Equal(Now, poll.OpenedAt);
    }

    [Fact]
    public void Open_InstantWithSubMicrosecondTicks_TruncatesToStoragePrecision()
    {
        var poll = Poll.Create(["Ana", "Bruno", "Carla"], Now.AddTicks(15)).Value;

        poll.Open(Now.AddTicks(27));

        Assert.Equal(Now.AddTicks(10), poll.CreatedAt);
        Assert.Equal(Now.AddTicks(20), poll.OpenedAt);
        Assert.Equal(Now.AddTicks(20).AddSeconds(30), poll.ClosesAt);
    }

    [Fact]
    public void StatusAt_NotOpened_ReturnsCreated()
    {
        var poll = CreatePoll();

        Assert.Equal(PollStatus.Created, poll.StatusAt(Now.AddMinutes(5)));
    }

    [Fact]
    public void StatusAt_OpenedAtAndClosesAt_ReturnsOpen()
    {
        var poll = CreatePoll();
        poll.Open(Now);

        Assert.Equal(PollStatus.Open, poll.StatusAt(Now));
        Assert.Equal(PollStatus.Open, poll.StatusAt(Now.AddSeconds(15)));
        Assert.Equal(PollStatus.Open, poll.StatusAt(Now.AddSeconds(30)));
    }

    [Fact]
    public void StatusAt_FinalizedPoll_ReturnsFinalized()
    {
        var poll = CreatePoll();
        poll.Open(Now);
        var finalized = Poll.Rehydrate(poll.Id, poll.CreatedAt, poll.OpenedAt, poll.ClosesAt, isFinalized: true, poll.Participants);

        Assert.Equal(PollStatus.Finalized, finalized.StatusAt(Now.AddMinutes(1)));
    }

    [Fact]
    public void Rehydrate_PersistedState_RebuildsSamePoll()
    {
        var original = CreatePoll();
        original.Open(Now.AddSeconds(5));

        var rehydrated = Poll.Rehydrate(
            original.Id,
            original.CreatedAt,
            original.OpenedAt,
            original.ClosesAt,
            isFinalized: false,
            original.Participants);

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(original.CreatedAt, rehydrated.CreatedAt);
        Assert.Equal(original.OpenedAt, rehydrated.OpenedAt);
        Assert.Equal(original.ClosesAt, rehydrated.ClosesAt);
        Assert.False(rehydrated.IsFinalized);
        Assert.Equal(original.Participants, rehydrated.Participants);
        Assert.Equal(original.StatusAt(Now.AddSeconds(10)), rehydrated.StatusAt(Now.AddSeconds(10)));
    }

    private static Poll CreatePoll()
    {
        return Poll.Create(["Ana", "Bruno", "Carla"], Now).Value;
    }
}
