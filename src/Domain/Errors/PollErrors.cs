using Domain.Common;

namespace Domain.Errors;

public static class PollErrors
{
    public static readonly DomainError InvalidParticipants = new(
        "invalid_participants",
        "A poll requires exactly 3 distinct, non-empty participant names of up to 100 characters.");

    public static readonly DomainError PollAlreadyOpened = new(
        "poll_already_opened",
        "The poll has already been opened.");
}
