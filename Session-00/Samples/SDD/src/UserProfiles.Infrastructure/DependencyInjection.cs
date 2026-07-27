using Microsoft.Extensions.DependencyInjection;
using UserProfiles.Application.Abstractions;

namespace UserProfiles.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUserProfilesInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IUserProfileRepository, InMemoryUserProfileRepository>();
        return services;
    }
}

