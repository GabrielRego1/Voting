namespace WebApi.Contracts.Requests;

public sealed record CreatePollRequest(IReadOnlyList<string>? Participants);
