using Voting.Domain.Common;

namespace Voting.Application.Common;

public static class ApplicationErrors
{
    public static readonly DomainError PollNotFound = new("poll_not_found", "The poll does not exist.");

    public static readonly DomainError AnotherPollOpen = new(
        "another_poll_open",
        "Another poll is open or has not been finalized yet.");
}
