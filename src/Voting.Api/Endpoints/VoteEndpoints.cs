using Microsoft.AspNetCore.Mvc;
using Voting.Api.Errors;
using Voting.Application.Votes;

namespace Voting.Api.Endpoints;

public sealed record CastVoteRequest(Guid ParticipantId);

public static class VoteEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapVoteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/polls/{pollId:guid}/votes", async (
            Guid pollId,
            [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
            CastVoteRequest? request,
            CastVote castVote,
            CancellationToken ct) =>
        {
            var result = await castVote.ExecuteAsync(pollId, request?.ParticipantId ?? Guid.Empty, idempotencyKey, ct);
            if (!result.IsSuccess)
            {
                return result.Error!.ToProblem();
            }

            return Results.Json(
                result.Value.Receipt,
                statusCode: result.Value.IsReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created);
        });

        return app;
    }
}
