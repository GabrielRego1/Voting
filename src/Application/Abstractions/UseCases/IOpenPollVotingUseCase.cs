using Application.Dtos;
using Domain.Common;

namespace Application.Abstractions.UseCases;

public interface IOpenPollVotingUseCase
{
    Task<Result<PollDto>> ExecuteAsync(Guid pollId, CancellationToken cancellationToken = default);
}
