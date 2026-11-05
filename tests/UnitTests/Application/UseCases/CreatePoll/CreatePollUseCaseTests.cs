using Application.UseCases.CreatePoll;
using Domain.Enums;
using Microsoft.Extensions.Time.Testing;
using UnitTests.Application.Fakes;

namespace UnitTests.Application.UseCases.CreatePoll;

public sealed class CreatePollUseCaseTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPollRepository _repository = new();

    [Fact]
    public async Task ExecuteAsync_ValidNames_PersistsAndReturnsCreatedPoll()
    {
        var sut = new CreatePollUseCase(_repository, _time);

        var result = await sut.ExecuteAsync(["Ana", "Bruno", "Carla"], TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(PollStatus.Created, dto.Status);
        Assert.Equal(_time.GetUtcNow(), dto.CreatedAt);
        Assert.Equal(["Ana", "Bruno", "Carla"], dto.Participants.Select(p => p.Name));
        Assert.NotNull(await _repository.GetAsync(dto.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidNames_FailsWithInvalidParticipantsAndPersistsNothing()
    {
        var sut = new CreatePollUseCase(_repository, _time);

        var result = await sut.ExecuteAsync(["Ana", "Bruno"], TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_participants", result.Error!.Code);
        Assert.Equal(0, _repository.Count);
    }
}
