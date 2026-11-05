using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// An immutable, append-only vote.
/// </summary>
public sealed record Vote(Guid Id, Guid PollId, Guid ParticipantId, string IdempotencyKey, DateTimeOffset ReceivedAt)
{
    public static Vote Create(Guid pollId, Guid participantId, string idempotencyKey, DateTimeOffset receivedAt)
    {
        var normalized = Instants.Normalize(receivedAt);
        return new Vote(Guid.CreateVersion7(normalized), pollId, participantId, idempotencyKey, normalized);
    }
}
