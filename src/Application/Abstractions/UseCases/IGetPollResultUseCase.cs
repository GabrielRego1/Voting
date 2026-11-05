using Application.UseCases.GetPollResult;
using Domain.Common;

namespace Application.Abstractions.UseCases;

public interface IGetPollResultUseCase
{
    Task<Result<PollResultDto>> ExecuteAsync(Guid pollId, CancellationToken cancellationToken = default);
}
