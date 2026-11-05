using Domain.Abstractions.Services;
using Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Domain;

public static class DependencyInjection
{
    public static IServiceCollection AddDomain(this IServiceCollection services) => services
        .AddSingleton<IResultCalculator, ResultCalculator>();
}
