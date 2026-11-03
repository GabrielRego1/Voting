using Microsoft.Extensions.DependencyInjection;
using Voting.Application.Polls;
using Voting.Application.Results;
using Voting.Application.Votes;

namespace Voting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<CreatePoll>();
        services.AddSingleton<OpenPoll>();
        services.AddSingleton<GetPoll>();
        services.AddSingleton<CastVote>();
        services.AddSingleton<FinalizePoll>();
        services.AddSingleton<GetResult>();
        return services;
    }
}
