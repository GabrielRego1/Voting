using Application.Abstractions;
using Domain.Common;
using Domain.Polls;

namespace Application.Polls;

public sealed class CreatePoll(IPollRepository polls, TimeProvider time)
{
    public async Task<Result<PollDto>> ExecuteAsync(IReadOnlyList<string>? participantNames, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var created = Poll.Create(participantNames, now);
        if (!created.IsSuccess)
        {
            return Result.Failure<PollDto>(created.Error!);
        }

        await polls.AddAsync(created.Value, cancellationToken);
        return Result.Success(PollDto.From(created.Value, now));
    }
}
