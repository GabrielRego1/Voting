using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Binds <typeparamref name="T"/> to the configuration section at <paramref name="configurationPath"/>, validates
    /// its DataAnnotations when the host starts, and registers <typeparamref name="T"/> itself with the current value.
    /// </summary>
    public static IServiceCollection ConfigureOptions<T>(this IServiceCollection services, string configurationPath)
        where T : class
        => services.AddOptions<T>()
            .BindConfiguration(configurationPath)
            .ValidateDataAnnotations()
            .ValidateOnStart()
            .Services
            .AddTransient<T>(sp => sp.GetRequiredService<IOptionsMonitor<T>>().CurrentValue);
}
