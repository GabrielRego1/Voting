using Application.UseCases.CastVote;
using Domain.Common;

namespace Application.Abstractions.UseCases;

/// <summary>
/// Validates and records a vote. Multiple votes per voter are allowed and no per-origin limit exists (FR-006, FR-018).
/// </summary>
public interface ICastVoteUseCase
{
    Task<Result<CastVoteResult>> ExecuteAsync(
        Guid pollId,
        Guid participantId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}
