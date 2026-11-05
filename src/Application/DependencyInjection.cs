using Application.Polls;
using Application.Results;
using Application.Votes;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

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
