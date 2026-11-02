using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Infra.DataAccess.Security;
using BlogSpecLab.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlogSpecLab.Tests.Integration;

public sealed class AuthorApiTests
{
    [Fact]
    public async Task SixRoutesProvideIndependentAuthorWorkflow()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        using var client = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        var created = await client.PostAsJsonAsync("/api/author/posts", new { title = "عنوان", body = "متن" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); NoStore(created);
        var p = await Json(created); var id = p.GetProperty("id").GetGuid();
        Assert.Equal(f.Users.Authors[0].UserId, p.GetProperty("ownerId").GetString());
        Assert.Equal("Draft", p.GetProperty("status").GetString());
        Assert.Equal(1, p.GetProperty("contentRevision").GetInt32());
        Assert.Equal($"/api/author/posts/{id}", created.Headers.Location!.ToString());
        Assert.Equal('"' + p.GetProperty("version").GetString()! + '"', created.Headers.ETag!.ToString());
        Actions(p, "edit", "delete", "submit");
        var get = await client.GetAsync(created.Headers.Location, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, get.StatusCode); NoStore(get);
        var edited = await Send(client, HttpMethod.Put, id, get.Headers.ETag!.ToString(), "{\"title\":\"تغییر\",\"body\":\"متن\"}");
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var submitted = await Send(client, HttpMethod.Post, id, edited.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode); Actions(await Json(submitted), "edit", "delete");
        await Problem(await Send(client, HttpMethod.Post, id, submitted.Headers.ETag!.ToString()), 409, "invalid_state");
        var deleted = await Send(client, HttpMethod.Delete, id, submitted.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode); NoStore(deleted);
        await Problem(await client.GetAsync($"/api/author/posts/{id}", TestContext.Current.CancellationToken), 404, "post_not_found");
        await Problem(await Send(client, HttpMethod.Post, id, submitted.Headers.ETag!.ToString()), 404, "post_not_found");
        Assert.Empty((await Json(await client.GetAsync("/api/author/posts", TestContext.Current.CancellationToken))).EnumerateArray());
    }

    [Theory]
    [InlineData("{}")] [InlineData("{\"title\":null,\"body\":\"ok\"}")]
    [InlineData("{\"title\":\" \u2003\",\"body\":\"ok\"}")]
    [InlineData("{\"title\":\"ok\",\"body\":\"\"}")]
    [InlineData("{\"title\":\"ok\",\"body\":\"ok\",\"ownerId\":\"forged\"}")]
    [InlineData("{\"title\":\"ok\",\"body\":\"ok\",\"status\":\"Published\"}")]
    [InlineData("{broken")]
    public async Task InvalidAndExtraFieldsAreRejected(string body)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        await Problem(await c.PostAsync("/api/author/posts", new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken), 400, "invalid_input");
        Assert.Empty((await Json(await c.GetAsync("/api/author/posts", TestContext.Current.CancellationToken))).EnumerateArray());
    }

    [Fact]
    public async Task IdentityPermissionOwnershipAndVersionErrorsDoNotLeakOrMutate()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        using var owner = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        using var other = f.CreateAuthenticatedClient(f.Users.Authors[1]);
        using var editor = f.CreateAuthenticatedClient(f.Users.Editors[0]);
        using var anonymous = f.CreateClient(new() { BaseAddress = new("https://localhost") });
        var created = await owner.PostAsJsonAsync("/api/author/posts", new { title = "عنوان خصوصی", body = "متن خصوصی" }, TestContext.Current.CancellationToken);
        var p = await Json(created); var id = p.GetProperty("id").GetGuid(); var etag = created.Headers.ETag!.ToString();
        var unauthorized = await anonymous.GetAsync("/api/author/posts", TestContext.Current.CancellationToken); await Problem(unauthorized, 401, "unauthenticated");
        Assert.Equal("Bearer", unauthorized.Headers.WwwAuthenticate.ToString());
        editor.DefaultRequestHeaders.Add("X-Role", "Author");
        await Problem(await editor.GetAsync("/api/author/posts", TestContext.Current.CancellationToken), 403, "forbidden");
        await Problem(await owner.GetAsync($"/api/author/posts/{Guid.NewGuid()}", TestContext.Current.CancellationToken), 404, "post_not_found");
        foreach (var response in new[] { await other.GetAsync($"/api/author/posts/{id}", TestContext.Current.CancellationToken),
            await Send(other, HttpMethod.Put, id, '\"' + Guid.NewGuid().ToString() + '\"', "{\"title\":\"new\",\"body\":\"body\"}"),
            await Send(other, HttpMethod.Delete, id, etag), await Send(other, HttpMethod.Post, id, etag) })
        {
            await Problem(response, 404, "post_not_found"); var error = await Json(response);
            Assert.False(error.TryGetProperty("currentStatus", out _));
            Assert.False(error.TryGetProperty("currentVersion", out _));
            Assert.False(error.TryGetProperty("allowedActions", out _));
            Assert.False(error.TryGetProperty("body", out _));
        }
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete, HttpMethod.Post })
        {
            var body = method == HttpMethod.Put ? "{\"title\":\"new\",\"body\":\"body\"}" : null;
            await Problem(await Send(owner, method, id, null, body), 428, "version_required");
            await Problem(await Send(owner, method, id, "*", body), 400, "invalid_input");
            var stale = await Send(owner, method, id, '\"' + Guid.NewGuid().ToString() + '\"', body);
            await Problem(stale, 412, "stale_version");
            Assert.Equal("Draft", (await Json(stale)).GetProperty("currentStatus").GetString());
        }
        await Problem(await Send(owner, HttpMethod.Post, id, etag, "{}"), 400, "invalid_input");
        await Problem(await Send(owner, HttpMethod.Delete, id, etag, "{}"), 400, "invalid_input");
        var unchanged = await owner.GetAsync($"/api/author/posts/{id}", TestContext.Current.CancellationToken);
        Assert.Equal(etag, unchanged.Headers.ETag!.ToString());
        Assert.Empty((await Json(await other.GetAsync("/api/author/posts", TestContext.Current.CancellationToken))).EnumerateArray());
    }

    [Theory]
    [InlineData(PostStatus.Draft, false)] [InlineData(PostStatus.PendingReview, false)]
    [InlineData(PostStatus.ChangesRequested, false)] [InlineData(PostStatus.Approved, false)]
    [InlineData(PostStatus.Published, false)] [InlineData(PostStatus.PendingReview, true)]
    [InlineData(PostStatus.Draft, true)] [InlineData(PostStatus.ChangesRequested, true)]
    [InlineData(PostStatus.Approved, true)] [InlineData(PostStatus.Published, true)]
    public async Task StateViewsAndAllowedActionsUseExactVocabularyAndOrder(PostStatus status, bool dualRole)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var user = dualRole ? f.Users.AuthorEditor : f.Users.Authors[0];
        var p = PostStates.In(status, user.UserId);
        await using (var scope = f.Services.CreateAsyncScope())
        { var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>(); await PostStates.SaveAsync(db, p); }
        using var c = f.CreateAuthenticatedClient(user);
        var response = await c.GetAsync($"/api/author/posts/{p.Id}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = await Json(response);
        var expected = status switch {
            PostStatus.Published => Array.Empty<string>(),
            PostStatus.Draft or PostStatus.ChangesRequested => ["edit", "delete", "submit"],
            PostStatus.PendingReview when dualRole => ["edit", "delete", "requestChanges"],
            PostStatus.Approved when dualRole => ["edit", "delete", "publish"],
            _ => new[] { "edit", "delete" } };
        Actions(view, expected);
        if (status == PostStatus.ChangesRequested)
        {
            var review = view.GetProperty("lastReview"); Assert.Equal("دلیل تاریخی", review.GetProperty("reason").GetString());
            Assert.False(review.TryGetProperty("reviewedBody", out _));
        }
        if (status == PostStatus.Published)
        {
            Assert.NotEqual(JsonValueKind.Null, view.GetProperty("publication").ValueKind);
            await Problem(await Send(c, HttpMethod.Put, p.Id, response.Headers.ETag!.ToString(), "{\"title\":\"new\",\"body\":\"body\"}"), 409, "published_post_immutable");
            await Problem(await Send(c, HttpMethod.Delete, p.Id, response.Headers.ETag!.ToString()), 409, "published_post_immutable");
        }
    }

    [Fact]
    public async Task ListsAreSortedByIdAndContainOnlyOwnNonDeletedIncludingPublished()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var owner = f.Users.Authors[0];
        var posts = new[] { PostStates.In(PostStatus.Published, owner.UserId), PostStates.In(PostStatus.Draft, owner.UserId), PostStates.In(PostStatus.Approved, owner.UserId) };
        var deleted = PostStates.In(PostStatus.Draft, owner.UserId); deleted.Delete();
        await using (var scope = f.Services.CreateAsyncScope())
        { var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>(); await PostStates.SaveAsync(db, posts.Concat(new[] { deleted, PostStates.In(PostStatus.Draft, f.Users.Authors[1].UserId) }).ToArray()); }
        using var c = f.CreateAuthenticatedClient(owner); var response = await c.GetAsync("/api/author/posts", TestContext.Current.CancellationToken); NoStore(response);
        var ids = (await Json(response)).EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(posts.Select(p => p.Id).OrderBy(x => x), ids);
    }

    [Fact]
    public async Task CurrentPermissionsAndCredentialRevocationAreRecheckedAfterAllowedActions()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        var created = await c.PostAsJsonAsync("/api/author/posts", new { title = "title", body = "body" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var view = await Json(created); Actions(view, "edit", "delete", "submit");
        var id = view.GetProperty("id").GetGuid();
        var ct = TestContext.Current.CancellationToken;
        Guid credentialId;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            credentialId = await db.Credentials.Where(x => x.UserId == f.Users.Authors[0].UserId).Select(x => x.Id).SingleAsync(ct);
            Assert.True((await scope.ServiceProvider.GetRequiredService<AccessProvisioner>().RevokePermissionsAsync(f.Users.Authors[0].UserId, ct)).IsSuccess);
        }
        await Problem(await Send(c, HttpMethod.Post, id, created.Headers.ETag!.ToString()), 403, "forbidden");
        await using (var scope = f.Services.CreateAsyncScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<AccessProvisioner>().RevokeCredentialAsync(credentialId, ct)).IsSuccess);
        await Problem(await c.GetAsync("/api/author/posts", ct), 401, "unauthenticated");
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var p = await scope.ServiceProvider.GetRequiredService<BlogDbContext>().Posts.SingleAsync(x => x.Id == id, ct);
            Assert.Equal(PostStatus.Draft, p.Status); Assert.Equal(view.GetProperty("version").GetGuid(), p.Version);
        }
    }

    [Theory]
    [InlineData(PostStatus.Draft)] [InlineData(PostStatus.PendingReview)]
    [InlineData(PostStatus.ChangesRequested)] [InlineData(PostStatus.Approved)]
    public async Task UnpublishedStatesSupportEditAndDeleteAndPreserveReviewHistory(PostStatus status)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var post = PostStates.In(status, f.Users.Authors[0].UserId);
        await using (var scope = f.Services.CreateAsyncScope())
            await PostStates.SaveAsync(scope.ServiceProvider.GetRequiredService<BlogDbContext>(), post);
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        var etag = '"' + post.Version.ToString() + '"';
        await Problem(await Send(c, HttpMethod.Put, post.Id, etag, "{\"title\":\"new\",\"body\":\"body\",\"role\":\"Editor\"}"), 400, "invalid_input");
        await Problem(await Send(c, HttpMethod.Put, post.Id, etag, "{\"title\":\" \u2003\",\"body\":\"body\"}"), 400, "invalid_input");
        var same = await Send(c, HttpMethod.Put, post.Id, etag, JsonSerializer.Serialize(new { title = post.Title, body = post.Body }));
        Assert.Equal(HttpStatusCode.OK, same.StatusCode); Assert.Equal(etag, same.Headers.ETag!.ToString());
        var edit = await Send(c, HttpMethod.Put, post.Id, etag, "{\"title\":\"new\",\"body\":\"body\"}");
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode); var view = await Json(edit);
        Assert.Equal(status == PostStatus.ChangesRequested ? "ChangesRequested" : "Draft", view.GetProperty("status").GetString());
        Assert.Equal(2, view.GetProperty("contentRevision").GetInt32());
        if (status == PostStatus.ChangesRequested) Assert.Equal("دلیل تاریخی", view.GetProperty("lastReview").GetProperty("reason").GetString());
        var delete = await Send(c, HttpMethod.Delete, post.Id, edit.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        await Problem(await c.GetAsync($"/api/author/posts/{post.Id}", TestContext.Current.CancellationToken), 404, "post_not_found");
    }

    internal static async Task Prepare(BlogApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>(); await db.Database.MigrateAsync();
        foreach (var user in factory.Users.Authors.Concat(factory.Users.Editors).Append(factory.Users.AuthorEditor))
        {
            db.UserAccess.Add(new() { UserId = user.UserId, CanWrite = user.CanWrite, CanReview = user.CanReview, CanPublish = user.CanPublish, IsActive = true });
            db.Credentials.Add(new() { Id = Guid.NewGuid(), UserId = user.UserId, TokenHash = user.TokenHash });
        }
        await db.SaveChangesAsync();
    }

    internal static async Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method, Guid id, string? etag, string? body = null)
    {
        using var request = new HttpRequestMessage(method, $"/api/author/posts/{id}" + (method == HttpMethod.Post ? "/submit" : ""));
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await c.SendAsync(request);
    }
    internal static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    internal static void NoStore(HttpResponseMessage response) => Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    internal static async Task Problem(HttpResponseMessage response, int status, string code)
    { Assert.Equal(status, (int)response.StatusCode); NoStore(response); Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType); Assert.Equal(code, (await Json(response)).GetProperty("code").GetString()); }
    private static void Actions(JsonElement p, params string[] expected) => Assert.Equal(expected, p.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()));
}
