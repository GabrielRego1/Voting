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
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddSingleton<ICreatePollUseCase, CreatePollUseCase>()
        .AddSingleton<IOpenPollVotingUseCase, OpenPollVotingUseCase>()
        .AddSingleton<IGetPollDetailsUseCase, GetPollDetailsUseCase>()
        .AddSingleton<ICastVoteUseCase, CastVoteUseCase>()
        .AddSingleton<IFinalizePollResultUseCase, FinalizePollResultUseCase>()
        .AddSingleton<IGetPollResultUseCase, GetPollResultUseCase>()
        .AddSingleton<IResultCalculator, ResultCalculator>();
}
