using Application.Abstractions.Persistence;
using Application.Dtos;
using Application.Tests.Fakes;
using Application.UseCases.CastVote;
using Application.UseCases.CreatePoll;
using Application.UseCases.OpenPollVoting;
using Microsoft.Extensions.Time.Testing;

namespace Application.Tests.Votes;

public sealed class CastVoteUseCaseTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPollRepository _polls = new();
    private readonly FakeVoteWriter _writer = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExecuteAsync_ValidVote_StampsReceivedAtFromTimeProviderBeforeWriting()
    {
        var poll = await CreateOpenPollAsync();
        _time.Advance(TimeSpan.FromSeconds(7));
        var expectedReceivedAt = _time.GetUtcNow();
        _writer.Respond = vote =>
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            return VoteWriteOutcome.Accepted(VoteReceipt.From(vote));
        };

        var result = await Sut().ExecuteAsync(poll.Id, poll.Participants[0].Id, "key-1", Ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsReplay);
        var written = Assert.Single(_writer.Writes);
        Assert.Equal(expectedReceivedAt, written.ReceivedAt);
        Assert.Equal(poll.Id, written.PollId);
        Assert.Equal(poll.Participants[0].Id, written.ParticipantId);
        Assert.Equal("key-1", written.IdempotencyKey);
        Assert.Equal(expectedReceivedAt, result.Value.Receipt.ReceivedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ExecuteAsync_MissingKey_FailsWithMissingIdempotencyKey(string? key)
    {
        var poll = await CreateOpenPollAsync();

        var result = await Sut().ExecuteAsync(poll.Id, poll.Participants[0].Id, key, Ct);

        Assert.Equal("missing_idempotency_key", result.Error!.Code);
        Assert.Empty(_writer.Writes);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("chave!")]
    [InlineData("ção")]
    public async Task ExecuteAsync_KeyWithInvalidCharacters_FailsWithInvalidIdempotencyKey(string key)
    {
        var poll = await CreateOpenPollAsync();

        var result = await Sut().ExecuteAsync(poll.Id, poll.Participants[0].Id, key, Ct);

        Assert.Equal("invalid_idempotency_key", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_KeyWith65Characters_FailsWithInvalidIdempotencyKey()
    {
        var poll = await CreateOpenPollAsync();

        var result = await Sut().ExecuteAsync(poll.Id, poll.Participants[0].Id, new string('k', 65), Ct);

        Assert.Equal("invalid_idempotency_key", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_KeyWith64AllowedCharacters_IsAccepted()
    {
        var poll = await CreateOpenPollAsync();
        var key = ("AZaz09-_" + new string('x', 56))[..64];

        var result = await Sut().ExecuteAsync(poll.Id, poll.Participants[0].Id, key, Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPoll_FailsWithPollNotFound()
    {
        var result = await Sut().ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), "key-1", Ct);

        Assert.Equal("poll_not_found", result.Error!.Code);
        Assert.Empty(_writer.Writes);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownParticipant_FailsWithInvalidParticipantWithoutWriting()
    {
        var poll = await CreateOpenPollAsync();

        var result = await Sut().ExecuteAsync(poll.Id, Guid.NewGuid(), "key-1", Ct);

        Assert.Equal("invalid_participant", result.Error!.Code);
        Assert.Empty(_writer.Writes);
    }

    [Fact]
    public async Task ExecuteAsync_WriterReplays_ReturnsOriginalReceiptAsReplay()
    {
        var poll = await CreateOpenPollAsync();
        var original = new VoteReceipt(Guid.NewGuid(), poll.Id, poll.Participants[0].Id, _time.GetUtcNow().AddSeconds(-1));
        _writer.Respond = _ => VoteWriteOutcome.Replayed(original);

        var result = await Sut().ExecuteAsync(poll.Id, poll.Participants[0].Id, "key-1", Ct);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsReplay);
        Assert.Equal(original, result.Value.Receipt);
    }

    [Fact]
    public async Task ExecuteAsync_WriterConflict_FailsWithIdempotencyKeyConflict()
    {
        var poll = await CreateOpenPollAsync();
        _writer.Respond = _ => VoteWriteOutcome.Conflict;

        var result = await Sut().ExecuteAsync(poll.Id, poll.Participants[0].Id, "key-1", Ct);

        Assert.Equal("idempotency_key_conflict", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WriterOverloaded_FailsWithOverloaded()
    {
        var poll = await CreateOpenPollAsync();
        _writer.Respond = _ => VoteWriteOutcome.Overloaded;

        var result = await Sut().ExecuteAsync(poll.Id, poll.Participants[0].Id, "key-1", Ct);

        Assert.Equal("overloaded", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_HundredVotesFromSameOrigin_AllAccepted()
    {
        var poll = await CreateOpenPollAsync();
        var sut = Sut();

        var results = new List<bool>();
        for (var i = 0; i < 100; i++)
        {
            var result = await sut.ExecuteAsync(poll.Id, poll.Participants[i % 3].Id, $"origin-1-{i}", Ct);
            results.Add(result.IsSuccess && !result.Value.IsReplay);
        }

        Assert.All(results, Assert.True);
        Assert.Equal(100, _writer.Writes.Count);
    }

    private CastVoteUseCase Sut()
    {
        return new CastVoteUseCase(_polls, _writer, _time);
    }

    private async Task<PollDto> CreateOpenPollAsync()
    {
        var created = await new CreatePollUseCase(_polls, _time).ExecuteAsync(["Ana", "Bruno", "Carla"], Ct);
        var opened = await new OpenPollVotingUseCase(_polls, _time).ExecuteAsync(created.Value.Id, Ct);
        return opened.Value;
    }
}
