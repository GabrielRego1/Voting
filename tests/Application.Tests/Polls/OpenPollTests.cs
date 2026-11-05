using Application.Polls;
using Application.Tests.Fakes;
using Domain.Enums;
using Microsoft.Extensions.Time.Testing;

namespace Application.Tests.Polls;

public sealed class OpenPollTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPollRepository _repository = new();

    [Fact]
    public async Task ExecuteAsync_CreatedPoll_OpensUsingTimeProvider()
    {
        var pollId = await CreatePollAsync();
        _time.Advance(TimeSpan.FromSeconds(3));
        var sut = new OpenPoll(_repository, _time);

        var result = await sut.ExecuteAsync(pollId, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(PollStatus.Open, result.Value.Status);
        Assert.Equal(_time.GetUtcNow(), result.Value.OpenedAt);
        Assert.Equal(_time.GetUtcNow().AddSeconds(30), result.Value.ClosesAt);
        var stored = await _repository.GetAsync(pollId, TestContext.Current.CancellationToken);
        Assert.Equal(_time.GetUtcNow(), stored!.OpenedAt);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyOpened_FailsWithPollAlreadyOpened()
    {
        var pollId = await CreatePollAsync();
        var sut = new OpenPoll(_repository, _time);
        await sut.ExecuteAsync(pollId, TestContext.Current.CancellationToken);

        var result = await sut.ExecuteAsync(pollId, TestContext.Current.CancellationToken);

        Assert.Equal("poll_already_opened", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_AnotherPollOpen_FailsWithAnotherPollOpen()
    {
        var first = await CreatePollAsync();
        var second = await CreatePollAsync();
        var sut = new OpenPoll(_repository, _time);
        await sut.ExecuteAsync(first, TestContext.Current.CancellationToken);

        var result = await sut.ExecuteAsync(second, TestContext.Current.CancellationToken);

        Assert.Equal("another_poll_open", result.Error!.Code);
        var stored = await _repository.GetAsync(second, TestContext.Current.CancellationToken);
        Assert.Null(stored!.OpenedAt);
    }

    [Fact]
    public async Task ExecuteAsync_AnotherPollClosedButNotFinalized_FailsWithAnotherPollOpen()
    {
        var first = await CreatePollAsync();
        var second = await CreatePollAsync();
        var sut = new OpenPoll(_repository, _time);
        await sut.ExecuteAsync(first, TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(1));

        var result = await sut.ExecuteAsync(second, TestContext.Current.CancellationToken);

        Assert.Equal("another_poll_open", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_AnotherPollFinalized_Opens()
    {
        var first = await CreatePollAsync();
        var second = await CreatePollAsync();
        var sut = new OpenPoll(_repository, _time);
        await sut.ExecuteAsync(first, TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(1));
        _repository.MarkFinalized(first);

        var result = await sut.ExecuteAsync(second, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPoll_FailsWithPollNotFound()
    {
        var sut = new OpenPoll(_repository, _time);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal("poll_not_found", result.Error!.Code);
    }

    private async Task<Guid> CreatePollAsync()
    {
        var created = await new CreatePoll(_repository, _time)
            .ExecuteAsync(["Ana", "Bruno", "Carla"], TestContext.Current.CancellationToken);
        return created.Value.Id;
    }
}
