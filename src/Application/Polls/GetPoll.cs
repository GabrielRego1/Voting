using Application.Abstractions;
using Application.Common;
using Domain.Common;

namespace Application.Polls;

public sealed class GetPoll(IPollRepository polls, TimeProvider time)
{
    public async Task<Result<PollDto>> ExecuteAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        var poll = await polls.GetAsync(pollId, cancellationToken);
        return poll is null
            ? Result.Failure<PollDto>(ApplicationErrors.PollNotFound)
            : Result.Success(PollDto.From(poll, time.GetUtcNow()));
    }
}
