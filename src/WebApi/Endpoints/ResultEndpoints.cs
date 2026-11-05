using Application.UseCases.GetPollResult;
using WebApi.Errors;

namespace WebApi.Endpoints;

public static class ResultEndpoints
{
    public static IEndpointRouteBuilder MapResultEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/polls/{pollId:guid}/result", async (Guid pollId, IGetPollResultUseCase getPollResult, CancellationToken ct) =>
        {
            var result = await getPollResult.ExecuteAsync(pollId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        });

        return app;
    }
}
