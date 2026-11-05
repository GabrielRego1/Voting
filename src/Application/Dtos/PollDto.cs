using Domain.Entities;
using Domain.Enums;

namespace Application.Dtos;

public sealed record PollDto(
    Guid Id,
    PollStatus Status,
    IReadOnlyList<ParticipantDto> Participants,
    DateTimeOffset CreatedAt,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? ClosesAt)
{
    public static PollDto From(Poll poll, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(poll);
        return new PollDto(
            poll.Id,
            poll.StatusAt(now),
            poll.Participants.Select(p => new ParticipantDto(p.Id, p.Name, p.Position)).ToArray(),
            poll.CreatedAt,
            poll.OpenedAt,
            poll.ClosesAt);
    }
}
