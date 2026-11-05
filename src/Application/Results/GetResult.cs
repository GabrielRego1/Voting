using Application.Abstractions;
using Application.Common;
using Domain.Common;

namespace Application.Results;

public sealed class GetResult(IPollRepository polls, IResultRepository results)
{
    public async Task<Result<PollResultDto>> ExecuteAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        var poll = await polls.GetAsync(pollId, cancellationToken);
        if (poll is null)
        {
            return Result.Failure<PollResultDto>(ApplicationErrors.PollNotFound);
        }

        var result = await results.GetAsync(pollId, cancellationToken);
        return result is null
            ? Result.Failure<PollResultDto>(ApplicationErrors.ResultNotAvailable)
            : Result.Success(PollResultDto.From(poll, result));
    }
}
