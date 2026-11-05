namespace Application.UseCases.GetPollResult;

public sealed record ParticipantTallyDto(Guid ParticipantId, string Name, long Votes, decimal Percentage);
