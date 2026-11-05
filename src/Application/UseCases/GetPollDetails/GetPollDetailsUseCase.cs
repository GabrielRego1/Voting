using Application.Abstractions.Persistence;
using Application.Dtos;
using Application.Errors;
using Domain.Common;

namespace Application.UseCases.GetPollDetails;

public sealed class GetPollDetailsUseCase(IPollRepository polls, TimeProvider time)
{
    public async Task<Result<PollDto>> ExecuteAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        var poll = await polls.GetAsync(pollId, cancellationToken);
        return poll is null
            ? Result.Failure<PollDto>(ApplicationErrors.PollNotFound)
            : Result.Success(PollDto.From(poll, time.GetUtcNow()));
    }
}
