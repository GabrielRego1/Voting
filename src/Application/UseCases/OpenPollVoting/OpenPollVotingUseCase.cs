using Application.Abstractions.Persistence;
using Application.Dtos;
using Application.Errors;
using Domain.Common;

namespace Application.UseCases.OpenPollVoting;

public sealed class OpenPollVotingUseCase(IPollRepository polls, TimeProvider time)
{
    public async Task<Result<PollDto>> ExecuteAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        var poll = await polls.GetAsync(pollId, cancellationToken);
        if (poll is null)
        {
            return Result.Failure<PollDto>(ApplicationErrors.PollNotFound);
        }

        var now = time.GetUtcNow();
        var opened = poll.Open(now);
        if (!opened.IsSuccess)
        {
            return Result.Failure<PollDto>(opened.Error!);
        }

        if (!await polls.TryOpenAsync(poll, cancellationToken))
        {
            return Result.Failure<PollDto>(ApplicationErrors.AnotherPollOpen);
        }

        return Result.Success(PollDto.From(poll, now));
    }
}
