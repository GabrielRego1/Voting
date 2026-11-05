using Application.UseCases.CastVote;
using Application.UseCases.CreatePoll;
using Application.UseCases.FinalizePollResult;
using Application.UseCases.GetPollDetails;
using Application.UseCases.GetPollResult;
using Application.UseCases.OpenPollVoting;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<CreatePollUseCase>();
        services.AddSingleton<OpenPollVotingUseCase>();
        services.AddSingleton<GetPollDetailsUseCase>();
        services.AddSingleton<CastVoteUseCase>();
        services.AddSingleton<FinalizePollResultUseCase>();
        services.AddSingleton<GetPollResultUseCase>();
        return services;
    }
}
