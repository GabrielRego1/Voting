namespace Application.Abstractions.UseCases;

/// <summary>
/// Tallies a closed poll once: drains the votes still queued, counts them and stores the result (research R-05).
/// </summary>
public interface IFinalizePollResultUseCase
{
    /// <summary>
    /// Returns <see langword="true"/> when this call stored the result.
    /// </summary>
    Task<bool> ExecuteAsync(Guid pollId, CancellationToken cancellationToken = default);
}
