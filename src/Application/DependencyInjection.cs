using Application.UseCases.CastVote;
using Application.UseCases.CreatePoll;
using Application.UseCases.FinalizePollResult;
using Application.UseCases.GetPollDetails;
using Application.UseCases.GetPollResult;
using Application.UseCases.OpenPollVoting;
using Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICreatePollUseCase, CreatePollUseCase>();
        services.AddSingleton<IOpenPollVotingUseCase, OpenPollVotingUseCase>();
        services.AddSingleton<IGetPollDetailsUseCase, GetPollDetailsUseCase>();
        services.AddSingleton<ICastVoteUseCase, CastVoteUseCase>();
        services.AddSingleton<IFinalizePollResultUseCase, FinalizePollResultUseCase>();
        services.AddSingleton<IGetPollResultUseCase, GetPollResultUseCase>();
        services.AddSingleton<IResultCalculator, ResultCalculator>();
        return services;
    }
}
