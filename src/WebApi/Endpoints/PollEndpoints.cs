using Application.UseCases.CreatePoll;
using Application.UseCases.GetPollDetails;
using Application.UseCases.OpenPollVoting;
using WebApi.Contracts.Requests;
using WebApi.Errors;

namespace WebApi.Endpoints;

public static class PollEndpoints
{
    public static IEndpointRouteBuilder MapPollEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/polls", async (CreatePollRequest? request, CreatePollUseCase createPoll, CancellationToken ct) =>
        {
            var result = await createPoll.ExecuteAsync(request?.Participants, ct);
            return result.IsSuccess
                ? Results.Created($"/polls/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        });

        app.MapGet("/polls/{pollId:guid}", async (Guid pollId, GetPollDetailsUseCase getPollDetails, CancellationToken ct) =>
        {
            var result = await getPollDetails.ExecuteAsync(pollId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        });

        app.MapPost("/polls/{pollId:guid}/open", async (Guid pollId, OpenPollVotingUseCase openPollVoting, CancellationToken ct) =>
        {
            var result = await openPollVoting.ExecuteAsync(pollId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        });

        return app;
    }
}
