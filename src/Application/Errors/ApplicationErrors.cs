using Domain.Common;

namespace Application.Errors;

public static class ApplicationErrors
{
    public static readonly DomainError PollNotFound = new("poll_not_found", "The poll does not exist.");

    public static readonly DomainError AnotherPollOpen = new(
        "another_poll_open",
        "Another poll is open or has not been finalized yet.");

    public static readonly DomainError ResultNotAvailable = new(
        "result_not_available",
        "The result is not available yet: the poll has not been opened, is open or is being finalized.");
}
