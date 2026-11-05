using Application.UseCases.CreatePoll;
using Application.UseCases.GetPollDetails;
using Application.UseCases.OpenPollVoting;
using Domain.Enums;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Application.Fakes;

namespace UnitTests.Application.UseCases.GetPollDetails;

public sealed class GetPollDetailsUseCaseTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPollRepository _repository = new();

    [Fact]
    public async Task ExecuteAsync_UnknownPoll_FailsWithPollNotFound()
    {
        var sut = new GetPollDetailsUseCase(_repository, _time);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal("poll_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingPoll_ReturnsStatusDerivedFromClock()
    {
        var created = await new CreatePollUseCase(_repository, _time)
            .ExecuteAsync(["Ana", "Bruno", "Carla"], TestContext.Current.CancellationToken);
        var sut = new GetPollDetailsUseCase(_repository, _time);

        var beforeOpen = await sut.ExecuteAsync(created.Value.Id, TestContext.Current.CancellationToken);
        await new OpenPollVotingUseCase(_repository, _time).ExecuteAsync(created.Value.Id, TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromSeconds(10));
        var afterOpen = await sut.ExecuteAsync(created.Value.Id, TestContext.Current.CancellationToken);

        Assert.Equal(PollStatus.Created, beforeOpen.Value.Status);
        Assert.Equal(PollStatus.Open, afterOpen.Value.Status);
        Assert.Equal(3, afterOpen.Value.Participants.Count);
    }
}
