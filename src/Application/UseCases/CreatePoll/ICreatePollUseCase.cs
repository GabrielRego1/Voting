using Application.Dtos;
using Domain.Common;

namespace Application.UseCases.CreatePoll;

public interface ICreatePollUseCase
{
    Task<Result<PollDto>> ExecuteAsync(IReadOnlyList<string>? participantNames, CancellationToken cancellationToken = default);
}
