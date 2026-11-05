using Application.Dtos;
using Application.UseCases.CreatePoll;
using Application.UseCases.FinalizePollResult;
using Application.UseCases.OpenPollVoting;
using Domain.Enums;
using Domain.Services;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Application.Fakes;

namespace UnitTests.Application.UseCases.FinalizePollResult;

public sealed class FinalizePollResultUseCaseTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPollRepository _polls = new();
    private readonly FakeVoteWriter _writer = new();
    private readonly FakeVoteTally _tally = new();
    private readonly InMemoryResultRepository _results;

    public FinalizePollResultUseCaseTests()
    {
        _results = new InMemoryResultRepository(_polls);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExecuteAsync_AfterClosesAt_FlushesUntilClosesAtBeforeCounting()
    {
        var poll = await CreateOpenPollAsync();
        var events = new List<string>();
        _writer.Events = events;
        _tally.Events = events;
        _time.SetUtcNow(poll.ClosesAt!.Value.AddMilliseconds(250));

        await Sut().ExecuteAsync(poll.Id, Ct);

        Assert.Equal([poll.ClosesAt.Value], _writer.Flushes);
        Assert.Equal(["flush", "count"], events);
    }

    [Fact]
    public async Task ExecuteAsync_AfterClosesAt_StoresResultWithFinalizedAtFromTimeProvider()
    {
        var poll = await CreateOpenPollAsync();
        _tally.Counts[poll.Participants[0].Id] = 6;
        _tally.Counts[poll.Participants[1].Id] = 3;
        _tally.Counts[poll.Participants[2].Id] = 1;
        _time.SetUtcNow(poll.ClosesAt!.Value.AddSeconds(1));

        await Sut().ExecuteAsync(poll.Id, Ct);

        var result = await _results.GetAsync(poll.Id, Ct);
        Assert.NotNull(result);
        Assert.Equal(_time.GetUtcNow(), result.FinalizedAt);
        Assert.Equal(10, result.TotalVotes);
        Assert.Equal(Outcome.Eliminated, result.Outcome);
        Assert.Equal(poll.Participants[0].Id, result.EliminatedParticipantId);
    }

    [Fact]
    public async Task ExecuteAsync_CalledTwice_KeepsTheFirstResult()
    {
        var poll = await CreateOpenPollAsync();
        _tally.Counts[poll.Participants[0].Id] = 1;
        _time.SetUtcNow(poll.ClosesAt!.Value.AddSeconds(1));
        await Sut().ExecuteAsync(poll.Id, Ct);
        var first = await _results.GetAsync(poll.Id, Ct);
        _tally.Counts[poll.Participants[1].Id] = 5;
        _time.Advance(TimeSpan.FromSeconds(10));

        await Sut().ExecuteAsync(poll.Id, Ct);

        Assert.Same(first, await _results.GetAsync(poll.Id, Ct));
    }

    [Fact]
    public async Task ExecuteAsync_AtOrBeforeClosesAt_DoesNothing()
    {
        var poll = await CreateOpenPollAsync();
        _time.SetUtcNow(poll.ClosesAt!.Value);

        await Sut().ExecuteAsync(poll.Id, Ct);

        Assert.Empty(_writer.Flushes);
        Assert.Null(await _results.GetAsync(poll.Id, Ct));
    }

    [Fact]
    public async Task ExecuteAsync_PollNeverOpened_DoesNothing()
    {
        var created = await new CreatePollUseCase(_polls, _time).ExecuteAsync(["Ana", "Bruno", "Carla"], Ct);
        _time.Advance(TimeSpan.FromMinutes(5));

        await Sut().ExecuteAsync(created.Value.Id, Ct);

        Assert.Empty(_writer.Flushes);
        Assert.Null(await _results.GetAsync(created.Value.Id, Ct));
    }

    private FinalizePollResultUseCase Sut()
    {
        return new FinalizePollResultUseCase(_polls, _writer, _tally, _results, new ResultCalculator(), _time);
    }

    private async Task<PollDto> CreateOpenPollAsync()
    {
        var created = await new CreatePollUseCase(_polls, _time).ExecuteAsync(["Ana", "Bruno", "Carla"], Ct);
        return (await new OpenPollVotingUseCase(_polls, _time).ExecuteAsync(created.Value.Id, Ct)).Value;
    }
}
