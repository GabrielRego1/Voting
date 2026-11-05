using Domain.Common;

namespace Application.UseCases.GetPollResult;

public interface IGetPollResultUseCase
{
    Task<Result<PollResultDto>> ExecuteAsync(Guid pollId, CancellationToken cancellationToken = default);
}
