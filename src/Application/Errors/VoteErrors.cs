using Domain.Common;

namespace Application.Errors;

public static class VoteErrors
{
    public static readonly DomainError MissingIdempotencyKey = new(
        "missing_idempotency_key",
        "The Idempotency-Key header is required.");

    public static readonly DomainError InvalidIdempotencyKey = new(
        "invalid_idempotency_key",
        "The Idempotency-Key must have 1-64 characters in [A-Za-z0-9-_].");

    public static readonly DomainError IdempotencyKeyConflict = new(
        "idempotency_key_conflict",
        "The Idempotency-Key was already used for a vote on a different participant.");

    public static readonly DomainError Overloaded = new(
        "overloaded",
        "The vote was not recorded because the system is overloaded; retry with the same Idempotency-Key.");
}
