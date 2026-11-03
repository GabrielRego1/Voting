using Microsoft.Extensions.Time.Testing;
using Voting.Application.Polls;
using Voting.Application.Results;
using Voting.Application.Tests.Fakes;
using Voting.Domain.Results;

namespace Voting.Application.Tests.Results;

public sealed class GetResultTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPollRepository _polls = new();
    private readonly InMemoryResultRepository _results;

    public GetResultTests()
    {
        _results = new InMemoryResultRepository(_polls);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExecuteAsync_UnknownPoll_FailsWithPollNotFound()
    {
        var result = await Sut().ExecuteAsync(Guid.NewGuid(), Ct);

        Assert.Equal("poll_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_CreatedOpenOrClosedWithoutResult_FailsWithResultNotAvailable()
    {
        var created = await new CreatePoll(_polls, _time).ExecuteAsync(["Ana", "Bruno", "Carla"], Ct);
        var pollId = created.Value.Id;

        var whenCreated = await Sut().ExecuteAsync(pollId, Ct);
        await new OpenPoll(_polls, _time).ExecuteAsync(pollId, Ct);
        var whenOpen = await Sut().ExecuteAsync(pollId, Ct);
        _time.Advance(TimeSpan.FromSeconds(31));
        var whenClosed = await Sut().ExecuteAsync(pollId, Ct);

        Assert.Equal("result_not_available", whenCreated.Error!.Code);
        Assert.Equal("result_not_available", whenOpen.Error!.Code);
        Assert.Equal("result_not_available", whenClosed.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_StoredResult_ReturnsResultWithParticipants()
    {
        var created = await new CreatePoll(_polls, _time).ExecuteAsync(["Ana", "Bruno", "Carla"], Ct);
        var poll = (await new OpenPoll(_polls, _time).ExecuteAsync(created.Value.Id, Ct)).Value;
        var p = poll.Participants;
        await _results.TryAddAsync(
            new PollResult(
                poll.Id,
                800,
                [
                    new ParticipantTally(p[0].Id, p[0].Name, 400, 50.00m),
                    new ParticipantTally(p[1].Id, p[1].Name, 400, 50.00m),
                    new ParticipantTally(p[2].Id, p[2].Name, 0, 0.00m),
                ],
                Outcome.Tie,
                null,
                [p[0].Id, p[1].Id],
                poll.ClosesAt!.Value.AddSeconds(1)),
            Ct);

        var result = await Sut().ExecuteAsync(poll.Id, Ct);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(800, dto.TotalVotes);
        Assert.Equal(Outcome.Tie, dto.Outcome);
        Assert.Null(dto.Eliminated);
        Assert.Equal([p[0], p[1]], dto.TiedParticipants);
        Assert.Equal([50.00m, 50.00m, 0.00m], dto.Tallies.Select(t => t.Percentage));
    }

    private GetResult Sut()
    {
        return new GetResult(_polls, _results);
    }
}
