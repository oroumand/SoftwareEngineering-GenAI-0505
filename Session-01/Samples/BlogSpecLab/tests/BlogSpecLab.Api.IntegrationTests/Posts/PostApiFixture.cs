using BlogSpecLab.Api.Posts.Identity;
using BlogSpecLab.Application.Posts.Ports;
using BlogSpecLab.Infrastructure.Posts.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace BlogSpecLab.Api.IntegrationTests.Posts;

public sealed class PostApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("blogspeclab")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public TestCurrentAuthorAccessor CurrentAuthor { get; } = new();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BlogSpecLab", _postgres.GetConnectionString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<BlogSpecLabDbContext>>();
                services.RemoveAll<IPostRepository>();
                services.RemoveAll<IUnitOfWork>();
                services.RemoveAll<IClock>();
                services.AddPostPersistence(_postgres.GetConnectionString());
                services.RemoveAll<ICurrentAuthorAccessor>();
                services.AddSingleton<ICurrentAuthorAccessor>(CurrentAuthor);
            });
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BlogSpecLabDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}

public sealed class TestCurrentAuthorAccessor : ICurrentAuthorAccessor
{
    public string? AuthorId { get; set; }

    public string? GetCurrentAuthorId() => AuthorId;
}
