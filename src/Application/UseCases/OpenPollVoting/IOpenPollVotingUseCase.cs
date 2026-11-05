using Application.Dtos;
using Domain.Common;

namespace Application.UseCases.OpenPollVoting;

public interface IOpenPollVotingUseCase
{
    Task<Result<PollDto>> ExecuteAsync(Guid pollId, CancellationToken cancellationToken = default);
}
