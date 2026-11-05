using Application.Polls;
using WebApi.Errors;

namespace WebApi.Endpoints;

public sealed record CreatePollRequest(IReadOnlyList<string>? Participants);

public static class PollEndpoints
{
    public static IEndpointRouteBuilder MapPollEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/polls", async (CreatePollRequest? request, CreatePoll createPoll, CancellationToken ct) =>
        {
            var result = await createPoll.ExecuteAsync(request?.Participants, ct);
            return result.IsSuccess
                ? Results.Created($"/polls/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        });

        app.MapGet("/polls/{pollId:guid}", async (Guid pollId, GetPoll getPoll, CancellationToken ct) =>
        {
            var result = await getPoll.ExecuteAsync(pollId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        });

        app.MapPost("/polls/{pollId:guid}/open", async (Guid pollId, OpenPoll openPoll, CancellationToken ct) =>
        {
            var result = await openPoll.ExecuteAsync(pollId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        });

        return app;
    }
}
