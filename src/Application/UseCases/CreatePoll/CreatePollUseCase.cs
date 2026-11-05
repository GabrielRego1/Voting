using Application.Abstractions.Persistence;
using Application.Abstractions.UseCases;
using Application.Dtos;
using Domain.Common;
using Domain.Entities;

namespace Application.UseCases.CreatePoll;

public sealed class CreatePollUseCase(IPollRepository polls, TimeProvider time) : ICreatePollUseCase
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
