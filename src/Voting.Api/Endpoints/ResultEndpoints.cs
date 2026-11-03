using Voting.Api.Errors;
using Voting.Application.Results;

namespace Voting.Api.Endpoints;

public static class ResultEndpoints
{
    public static IEndpointRouteBuilder MapResultEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/polls/{pollId:guid}/result", async (Guid pollId, GetResult getResult, CancellationToken ct) =>
        {
            var result = await getResult.ExecuteAsync(pollId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        });

        return app;
    }
}
