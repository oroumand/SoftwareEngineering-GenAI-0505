using System.Net;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Tests.Fixtures;
using Xunit;
using static BlogSpecLab.Tests.Integration.AuthorApiTests;
using static BlogSpecLab.Tests.Integration.Us3Support;

namespace BlogSpecLab.Tests.Integration;

public sealed class PublicApiTests
{
    [Fact]
    public async Task AnonymousListAndDetailExposeOnlyPublishedProjectionSortedById()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var posts = Enum.GetValues<PostStatus>().Select(s => PostStates.In(s, f.Users.Authors[0].UserId)).ToArray();
        var second = PostStates.In(PostStatus.Published, f.Users.Authors[1].UserId);
        var deleted = PostStates.In(PostStatus.Published, f.Users.Authors[0].UserId);
        typeof(Post).GetProperty(nameof(Post.IsDeleted))!.SetValue(deleted, true);
        await Save(f, posts.Append(second).Append(deleted).ToArray());
        using var c = f.CreateClient(new() { BaseAddress = new("https://localhost") });
        var response = await c.GetAsync("/api/posts", Ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await Json(response)).EnumerateArray().ToArray();
        var published = posts.Where(p => p.Status == PostStatus.Published).Append(second).OrderBy(p => p.Id).ToArray();
        Assert.Equal(published.Select(p => p.Id), list.Select(p => p.GetProperty("id").GetGuid()));
        foreach (var post in published)
        {
            var detail = await c.GetAsync($"/api/posts/{post.Id}", Ct); Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            Assert.Null(detail.Headers.ETag);
            var view = await Json(detail);
            Assert.Equal(new[] { "body", "id", "publishedAt", "title" }, view.EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(post.Title, view.GetProperty("title").GetString()); Assert.Equal(post.Body, view.GetProperty("body").GetString());
            Assert.Equal(post.Publication!.PublishedAt, view.GetProperty("publishedAt").GetDateTime());
            Assert.Equal(view.GetRawText(), list.Single(p => p.GetProperty("id").GetGuid() == post.Id).GetRawText());
        }
    }
    [Fact]
    public async Task PrivateDeletedAndMissingHaveIdenticalProblemsApartFromRequestedInstance()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var privatePosts = Enum.GetValues<PostStatus>().Where(s => s != PostStatus.Published)
            .Select(s => PostStates.In(s, f.Users.Authors[0].UserId)).ToArray();
        var deleted = PostStates.In(PostStatus.Published, f.Users.Authors[0].UserId);
        typeof(Post).GetProperty(nameof(Post.IsDeleted))!.SetValue(deleted, true);
        await Save(f, privatePosts.Append(deleted).ToArray());
        using var c = f.CreateClient(new() { BaseAddress = new("https://localhost") });
        string? canonical = null;
        foreach (var id in privatePosts.Select(p => p.Id).Append(deleted.Id).Append(Guid.NewGuid()))
        {
            var response = await c.GetAsync($"/api/posts/{id}", Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            await Problem(response, 404, "post_not_found");
            var error = await Json(response);
            var fields = error.EnumerateObject().Where(p => p.Name != "instance").Select(p => p.Name + ":" + p.Value.GetRawText());
            var normalized = string.Join("|", fields);
            canonical ??= normalized; Assert.Equal(canonical, normalized);
            Assert.Equal(new[] { "code", "detail", "instance", "status", "title", "type" }, error.EnumerateObject().Select(p => p.Name).Order());
        }
    }
}
