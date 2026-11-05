using Application.Polls;
using Application.Tests.Fakes;
using Domain.Enums;
using Microsoft.Extensions.Time.Testing;

namespace Application.Tests.Polls;

public sealed class GetPollTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPollRepository _repository = new();

    [Fact]
    public async Task ExecuteAsync_UnknownPoll_FailsWithPollNotFound()
    {
        var sut = new GetPoll(_repository, _time);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal("poll_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingPoll_ReturnsStatusDerivedFromClock()
    {
        var created = await new CreatePoll(_repository, _time)
            .ExecuteAsync(["Ana", "Bruno", "Carla"], TestContext.Current.CancellationToken);
        var sut = new GetPoll(_repository, _time);

        var beforeOpen = await sut.ExecuteAsync(created.Value.Id, TestContext.Current.CancellationToken);
        await new OpenPoll(_repository, _time).ExecuteAsync(created.Value.Id, TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromSeconds(10));
        var afterOpen = await sut.ExecuteAsync(created.Value.Id, TestContext.Current.CancellationToken);

        Assert.Equal(PollStatus.Created, beforeOpen.Value.Status);
        Assert.Equal(PollStatus.Open, afterOpen.Value.Status);
        Assert.Equal(3, afterOpen.Value.Participants.Count);
    }
}
