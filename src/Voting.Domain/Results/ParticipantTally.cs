namespace Voting.Domain.Results;

/// <summary>
/// Votes of one participant; <c>Percentage</c> has exactly two decimal places.
/// </summary>
public sealed record ParticipantTally(Guid ParticipantId, string Name, long Votes, decimal Percentage);
