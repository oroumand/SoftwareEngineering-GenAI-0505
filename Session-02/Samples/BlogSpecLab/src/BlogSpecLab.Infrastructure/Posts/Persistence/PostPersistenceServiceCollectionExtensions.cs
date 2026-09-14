using BlogSpecLab.Application.Posts.Ports;
using BlogSpecLab.Infrastructure.Posts.Repositories;
using BlogSpecLab.Infrastructure.Posts.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlogSpecLab.Infrastructure.Posts.Persistence;

public static class PostPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPostPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BlogSpecLabDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IPostRepository, EfPostRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IClock, UtcClock>();
        return services;
    }
}
