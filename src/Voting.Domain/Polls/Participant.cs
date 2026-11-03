namespace Voting.Domain.Polls;

public sealed record Participant(Guid Id, string Name, int Position);
