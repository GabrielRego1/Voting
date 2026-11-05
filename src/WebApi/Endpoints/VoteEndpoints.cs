using Application.Abstractions.UseCases;
using Microsoft.AspNetCore.Mvc;
using WebApi.Contracts.Requests;
using WebApi.Errors;

namespace WebApi.Endpoints;

public static class VoteEndpoints
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapVoteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/polls/{pollId:guid}/votes", async (
            Guid pollId,
            [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
            CastVoteRequest? request,
            ICastVoteUseCase castVote,
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
