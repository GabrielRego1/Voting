using System.Text.Json.Serialization;

namespace WebApi;

public static class DependencyInjection
{
    public static IServiceCollection AddWebApi(this IServiceCollection services) => services
        .AddSingleton(TimeProvider.System)
        .AddProblemDetails()
        .ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
}
