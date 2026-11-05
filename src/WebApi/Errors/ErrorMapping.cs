using System.Globalization;
using Domain.Common;

namespace WebApi.Errors;

/// <summary>
/// Maps every stable error <c>code</c> of the API contract to an RFC 9457 Problem Details response.
/// </summary>
public static class ErrorMapping
{
    private const int RetryAfterSeconds = 1;

    private static readonly Dictionary<string, int> StatusByCode = new(StringComparer.Ordinal)
    {
        ["invalid_participants"] = StatusCodes.Status400BadRequest,
        ["missing_idempotency_key"] = StatusCodes.Status400BadRequest,
        ["invalid_idempotency_key"] = StatusCodes.Status400BadRequest,
        ["poll_not_found"] = StatusCodes.Status404NotFound,
        ["poll_already_opened"] = StatusCodes.Status409Conflict,
        ["another_poll_open"] = StatusCodes.Status409Conflict,
        ["voting_not_open"] = StatusCodes.Status409Conflict,
        ["voting_closed"] = StatusCodes.Status409Conflict,
        ["idempotency_key_conflict"] = StatusCodes.Status409Conflict,
        ["result_not_available"] = StatusCodes.Status409Conflict,
        ["invalid_participant"] = StatusCodes.Status422UnprocessableEntity,
        ["overloaded"] = StatusCodes.Status503ServiceUnavailable,
    };

    private static int StatusCodeFor(string code)
    {
        return StatusByCode.TryGetValue(code, out var status) ? status : StatusCodes.Status500InternalServerError;
    }

    public static IResult ToProblem(this DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var status = StatusCodeFor(error.Code);
        var problem = TypedResults.Problem(
            statusCode: status,
            title: error.Code,
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });

        return status == StatusCodes.Status503ServiceUnavailable
            ? new RetryAfterResult(problem, RetryAfterSeconds)
            : problem;
    }

    private sealed class RetryAfterResult(IResult inner, int seconds) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            return inner.ExecuteAsync(httpContext);
        }
    }
}
