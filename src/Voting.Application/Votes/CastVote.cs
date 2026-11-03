using Voting.Application.Abstractions;
using Voting.Application.Common;
using Voting.Domain.Common;
using Voting.Domain.Votes;

namespace Voting.Application.Votes;

public sealed record CastVoteResult(VoteReceipt Receipt, bool IsReplay);

/// <summary>
/// Validates and records a vote. Multiple votes per voter are allowed and no per-origin limit exists (FR-006, FR-018).
/// </summary>
public sealed class CastVote(IPollRepository polls, IVoteWriter writer, TimeProvider time)
{
    public const int MaxIdempotencyKeyLength = 64;

    public async Task<Result<CastVoteResult>> ExecuteAsync(
        Guid pollId,
        Guid participantId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        // Stamped on arrival, before any I/O, so time spent queued never counts against the voter (research R-04).
        var receivedAt = time.GetUtcNow();

        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return Result.Failure<CastVoteResult>(VoteErrors.MissingIdempotencyKey);
        }

        if (!IsValidKey(idempotencyKey))
        {
            return Result.Failure<CastVoteResult>(VoteErrors.InvalidIdempotencyKey);
        }

        var poll = await polls.GetAsync(pollId, cancellationToken);
        if (poll is null)
        {
            return Result.Failure<CastVoteResult>(ApplicationErrors.PollNotFound);
        }

        var evaluation = poll.EvaluateVote(participantId, receivedAt);
        if (!evaluation.IsAccepted)
        {
            return Result.Failure<CastVoteResult>(evaluation.Rejection!);
        }

        var outcome = await writer.WriteAsync(Vote.Create(pollId, participantId, idempotencyKey, receivedAt), cancellationToken);
        return outcome.Status switch
        {
            VoteWriteStatus.Accepted => Result.Success(new CastVoteResult(outcome.Receipt!, IsReplay: false)),
            VoteWriteStatus.Replayed => Result.Success(new CastVoteResult(outcome.Receipt!, IsReplay: true)),
            VoteWriteStatus.Conflict => Result.Failure<CastVoteResult>(VoteErrors.IdempotencyKeyConflict),
            VoteWriteStatus.Overloaded => Result.Failure<CastVoteResult>(VoteErrors.Overloaded),
            _ => throw new InvalidOperationException($"Unexpected vote write status '{outcome.Status}'."),
        };
    }

    private static bool IsValidKey(string key)
    {
        if (key.Length > MaxIdempotencyKeyLength)
        {
            return false;
        }

        foreach (var c in key)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
