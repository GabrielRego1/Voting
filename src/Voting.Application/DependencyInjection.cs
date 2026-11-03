using Microsoft.Extensions.DependencyInjection;
using Voting.Application.Polls;

namespace Voting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<CreatePoll>();
        services.AddSingleton<OpenPoll>();
        services.AddSingleton<GetPoll>();
        return services;
    }
}
