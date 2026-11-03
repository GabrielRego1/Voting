using Voting.Domain.Common;

namespace Voting.Domain.Polls;

/// <summary>
/// The "paredão": exactly three participants and a voting window of <see cref="Duration"/>.
/// The status is always derived from the clock and is never stored as a mutable flag.
/// </summary>
public sealed class Poll
{
    public const int ParticipantCount = 3;
    public const int MaxNameLength = 100;

    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    private Poll(
        Guid id,
        DateTimeOffset createdAt,
        DateTimeOffset? openedAt,
        DateTimeOffset? closesAt,
        bool isFinalized,
        IReadOnlyList<Participant> participants)
    {
        Id = id;
        CreatedAt = createdAt;
        OpenedAt = openedAt;
        ClosesAt = closesAt;
        IsFinalized = isFinalized;
        Participants = participants;
    }

    public Guid Id { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? OpenedAt { get; private set; }

    public DateTimeOffset? ClosesAt { get; private set; }

    public bool IsFinalized { get; }

    public IReadOnlyList<Participant> Participants { get; }

    public static Result<Poll> Create(IReadOnlyList<string>? names, DateTimeOffset createdAt)
    {
        createdAt = Instants.Normalize(createdAt);
        if (names is null || names.Count != ParticipantCount)
        {
            return Result.Failure<Poll>(PollErrors.InvalidParticipants);
        }

        var trimmed = names.Select(n => n?.Trim() ?? string.Empty).ToArray();
        if (trimmed.Any(n => n.Length is 0 or > MaxNameLength)
            || trimmed.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ParticipantCount)
        {
            return Result.Failure<Poll>(PollErrors.InvalidParticipants);
        }

        var participants = trimmed
            .Select((name, index) => new Participant(Guid.CreateVersion7(createdAt), name, index + 1))
            .ToArray();

        return Result.Success(new Poll(Guid.CreateVersion7(createdAt), createdAt, null, null, false, participants));
    }

    public static Poll Rehydrate(
        Guid id,
        DateTimeOffset createdAt,
        DateTimeOffset? openedAt,
        DateTimeOffset? closesAt,
        bool isFinalized,
        IReadOnlyList<Participant> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        return new Poll(id, createdAt, openedAt, closesAt, isFinalized, participants.OrderBy(p => p.Position).ToArray());
    }

    public Result Open(DateTimeOffset now)
    {
        if (OpenedAt is not null)
        {
            return Result.Failure(PollErrors.PollAlreadyOpened);
        }

        OpenedAt = Instants.Normalize(now);
        ClosesAt = OpenedAt + Duration;
        return Result.Success();
    }

    public PollStatus StatusAt(DateTimeOffset now)
    {
        _ = now;
        return OpenedAt is null ? PollStatus.Created : PollStatus.Open;
    }
}
