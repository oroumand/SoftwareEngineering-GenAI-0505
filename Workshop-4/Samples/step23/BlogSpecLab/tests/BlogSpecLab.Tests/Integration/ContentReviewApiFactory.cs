using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace BlogSpecLab.Tests.Integration;
public sealed class ContentReviewApiFactory : BlogApiFactory
{
    public ContentReviewFake Gateway { get; } = new();
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    public bool UseConfiguredGateway { get; set; }
    public Dictionary<string, string?> Settings { get; } = [];
    public Action<IServiceCollection>? OverrideServices { get; set; }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        foreach (var setting in Settings) builder.UseSetting(setting.Key, setting.Value);
        builder.ConfigureServices(services => {
            services.RemoveAll<TimeProvider>(); services.AddSingleton(Clock);
            if (!UseConfiguredGateway) { services.RemoveAll<IContentReviewGateway>(); services.AddSingleton<IContentReviewGateway>(Gateway); }
            OverrideServices?.Invoke(services);
        });
    }
    public async Task<Post> Prepare(PostStatus status = PostStatus.Draft, string? owner = null)
    {
        await AuthorApiTests.Prepare(this);
        var post = PostStates.In(status, owner ?? Users.Authors[0].UserId);
        await Us3Support.Save(this, post); return post;
    }
    public async Task Change(Guid id, Action<Post> change)
    {
        await using var db = Us3Support.Context(this);
        var post = await db.Posts.Include(p => p.Reviews).SingleAsync(p => p.Id == id);
        change(post); await db.SaveChangesAsync();
    }
    public static string Route(Guid id) => $"/api/author/posts/{id}/content-review";
}
