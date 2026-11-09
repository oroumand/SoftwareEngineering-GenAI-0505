using System.Net;
using System.Text;
using System.Text.Json;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Core.Application.Persistence;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Infra.DataAccess.Security;
using BlogSpecLab.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static BlogSpecLab.Tests.Integration.AuthorApiTests;
namespace BlogSpecLab.Tests.Integration;
public sealed class EditorApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task IndependentReturnResubmitApprovePublishWorkflowAndHistoricalRevocation()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(PostStatus.PendingReview, f.Users.AuthorEditor.UserId); await Save(f, p);
        using var editor = f.CreateAuthenticatedClient(f.Users.Editors[0]); using var owner = f.CreateAuthenticatedClient(f.Users.AuthorEditor);
        var detail = await editor.GetAsync($"/api/editor/posts/{p.Id}", Ct); Assert.Equal(HttpStatusCode.OK, detail.StatusCode); NoStore(detail);
        Actions(await Json(detail), "requestChanges", "approve");
        var ownView = await owner.GetAsync($"/api/editor/posts/{p.Id}", Ct); Actions(await Json(ownView), "edit", "delete", "requestChanges");
        await Problem(await Command(owner, p.Id, "approve", Tag(p)), 403, "self_approval_forbidden");
        var returned = await Command(editor, p.Id, "request-changes", Tag(p), "{\"reason\":\"اصلاح لازم است\"}");
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode); Actions(await Json(returned));
        Assert.Equal("ChangesRequested", (await Json(returned)).GetProperty("status").GetString());
        var authorView = await owner.GetAsync($"/api/author/posts/{p.Id}", Ct); Actions(await Json(authorView), "edit", "delete", "submit");
        Assert.Equal("اصلاح لازم است", (await Json(authorView)).GetProperty("lastReview").GetProperty("reason").GetString());
        var edit = await Send(owner, HttpMethod.Put, p.Id, returned.Headers.ETag!.ToString(), "{\"title\":\"new\",\"body\":\"new body\"}");
        var submit = await Send(owner, HttpMethod.Post, p.Id, edit.Headers.ETag!.ToString());
        var approved = await Command(editor, p.Id, "approve", submit.Headers.ETag!.ToString()); Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var view = await Json(approved); Actions(view, "publish"); Assert.Equal(JsonValueKind.Null, view.GetProperty("publication").ValueKind);
        Assert.Equal("Approved", view.GetProperty("status").GetString()); var review = view.GetProperty("lastReview");
        Assert.Equal(f.Users.Editors[0].UserId, review.GetProperty("reviewerId").GetString()); Assert.Equal(2, review.GetProperty("contentRevision").GetInt32());
        Assert.False(review.TryGetProperty("reviewedBody", out _));
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var access = scope.ServiceProvider.GetRequiredService<AccessProvisioner>();
            Assert.True((await access.RevokePermissionsAsync(f.Users.Editors[0].UserId, Ct)).IsSuccess);
        }
        await Problem(await Command(editor, p.Id, "publish", approved.Headers.ETag!.ToString()), 403, "forbidden");
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var credentialId = await scope.ServiceProvider.GetRequiredService<BlogDbContext>().Credentials.Where(c => c.UserId == f.Users.Editors[0].UserId).Select(c => c.Id).SingleAsync(Ct);
            Assert.True((await scope.ServiceProvider.GetRequiredService<AccessProvisioner>().RevokeCredentialAsync(credentialId, Ct)).IsSuccess);
        }
        var published = await Command(owner, p.Id, "publish", approved.Headers.ETag!.ToString()); Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var publication = await Json(published); Actions(publication); Assert.Equal("Published", publication.GetProperty("status").GetString());
        Assert.Equal(f.Users.AuthorEditor.UserId, publication.GetProperty("publication").GetProperty("publisherId").GetString());
        var again = await Command(owner, p.Id, "publish", published.Headers.ETag!.ToString()); Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(publication.GetRawText(), (await Json(again)).GetRawText());
        await Problem(await owner.GetAsync($"/api/editor/posts/{p.Id}", Ct), 404, "post_not_found");
        await using var verify = f.Services.CreateAsyncScope(); var db = verify.ServiceProvider.GetRequiredService<BlogDbContext>();
        var stored = await db.Posts.Include(x => x.Reviews).SingleAsync(x => x.Id == p.Id, Ct);
        Assert.Equal(2, stored.Reviews.Count); Assert.True(stored.HasValidApproval()); Assert.Equal(DateTimeKind.Utc, stored.Publication!.PublishedAt.Kind);
        Assert.Equal(stored.CurrentReviewId, stored.Publication.ApprovedReviewId);
    }
    [Fact]
    public async Task QueueIsSortedAndDetailScopeAndActionsAreExact()
    {
        await using var f = new BlogApiFactory(); await Prepare(f); var user = f.Users.AuthorEditor;
        var posts = Enum.GetValues<PostStatus>().Select(s => PostStates.In(s, user.UserId)).ToArray();
        var deleted = PostStates.In(PostStatus.PendingReview, user.UserId); deleted.Delete(); await Save(f, posts.Append(deleted).ToArray());
        using var e = f.CreateAuthenticatedClient(f.Users.Editors[0]); using var o = f.CreateAuthenticatedClient(user);
        var list = await e.GetAsync("/api/editor/posts", Ct); Assert.Equal(HttpStatusCode.OK, list.StatusCode); NoStore(list);
        Assert.Equal(posts.Where(p => p.Status is PostStatus.PendingReview or PostStatus.Approved).Select(p => p.Id).OrderBy(id => id), (await Json(list)).EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
        foreach (var p in posts.Append(deleted))
        {
            var detail = await e.GetAsync($"/api/editor/posts/{p.Id}", Ct);
            if (p.IsDeleted || p.Status is not (PostStatus.PendingReview or PostStatus.Approved)) { await Problem(detail, 404, "post_not_found"); continue; }
            Assert.Equal(Tag(p), detail.Headers.ETag!.ToString());
            Actions(await Json(detail), p.Status == PostStatus.PendingReview ? ["requestChanges", "approve"] : ["publish"]);
            var own = await o.GetAsync($"/api/editor/posts/{p.Id}", Ct);
            Actions(await Json(own), p.Status == PostStatus.PendingReview ? ["edit", "delete", "requestChanges"] : ["edit", "delete", "publish"]);
        }
        var invalid = PostStates.In(PostStatus.Approved, user.UserId); invalid.Edit("changed", invalid.Body);
        typeof(Post).GetProperty(nameof(Post.Status))!.SetValue(invalid, PostStatus.Approved); await Save(f, invalid);
        Actions(await Json(await e.GetAsync($"/api/editor/posts/{invalid.Id}", Ct)));
        await Problem(await Command(e, invalid.Id, "publish", Tag(invalid)), 409, "approval_required");
    }
    [Fact]
    public async Task ContractInputVersionPermissionAndSelfApprovalFailuresLeaveDatabaseUntouched()
    {
        await using var f = new BlogApiFactory(); await Prepare(f); var p = PostStates.In(PostStatus.PendingReview, f.Users.AuthorEditor.UserId); await Save(f, p);
        using var e = f.CreateAuthenticatedClient(f.Users.Editors[0]); using var author = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        using var anonymous = f.CreateClient(new() { BaseAddress = new("https://localhost") });
        await Problem(await anonymous.GetAsync("/api/editor/posts", Ct), 401, "unauthenticated");
        await Problem(await author.GetAsync("/api/editor/posts", Ct), 403, "forbidden");
        foreach (var action in new[] { "approve", "publish", "request-changes" })
        {
            var body = action == "request-changes" ? "{\"reason\":\"reason\"}" : null;
            await Problem(await Command(e, p.Id, action, null, body), 428, "version_required");
            await Problem(await Command(e, p.Id, action, "*", body), 400, "invalid_input");
            await Problem(await Command(e, p.Id, action, '"' + Guid.NewGuid().ToString() + '"', body), 412, "stale_version");
            await Problem(await Command(author, p.Id, action, Tag(p), body), 403, "forbidden");
        }
        foreach (var body in new[] { "{}", "{\"reason\":null}", "{\"reason\":\" \\t\"}", "{broken", "{\"reason\":\"ok\",\"reviewerId\":\"forged\"}" })
            await Problem(await Command(e, p.Id, "request-changes", Tag(p), body), 400, "invalid_input");
        await Problem(await Command(e, p.Id, "approve", Tag(p), "{}"), 400, "invalid_input");
        await Problem(await Command(e, p.Id, "publish", Tag(p), "{}"), 400, "invalid_input");
        await Problem(await Command(e, p.Id, "publish", Tag(p)), 409, "invalid_state");
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var stored = await db.Posts.Include(x => x.Reviews).SingleAsync(Ct); Assert.Equal(p.Version, stored.Version); Assert.Empty(stored.Reviews); Assert.Null(stored.Publication);
    }
    [Fact]
    public async Task RollbackOfReviewCommitPreservesPostAndNoDecisionAndHistoryCannotBeChanged()
    {
        await using var f = new BlogApiFactory(); await Prepare(f); var p = PostStates.In(PostStatus.PendingReview, f.Users.Authors[0].UserId); await Save(f, p);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>(); var session = scope.ServiceProvider.GetRequiredService<IWriteSession>();
            Assert.True((await session.BeginAsync(Ct)).IsSuccess);
            var stored = await db.Posts.Include(x => x.Reviews).SingleAsync(Ct); stored.Approve(f.Users.Editors[0].UserId, DateTime.UtcNow);
            db.ReviewDecisions.Add(stored.Reviews.Single());
            await db.SaveChangesAsync(Ct); await session.RollbackAsync(Ct);
        }
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>(); Assert.Empty(await db.ReviewDecisions.ToArrayAsync(Ct));
            var stored = await db.Posts.Include(x => x.Reviews).SingleAsync(Ct); Assert.Equal(p.Version, stored.Version); Assert.Equal(PostStatus.PendingReview, stored.Status);
        }
        using var e = f.CreateAuthenticatedClient(f.Users.Editors[0]); Assert.Equal(HttpStatusCode.OK, (await Command(e, p.Id, "approve", Tag(p))).StatusCode);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>(); var review = await db.ReviewDecisions.SingleAsync(Ct);
            db.Entry(review).Property(x => x.Reason).CurrentValue = "mutated";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }
    }
    [Fact]
    public async Task FailedReviewCommitRollsBackPostAndDecisionTogether()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(PostStatus.PendingReview, f.Users.Authors[0].UserId); await Save(f, p);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_review BEFORE INSERT ON ReviewDecisions BEGIN SELECT RAISE(ABORT, 'test failure'); END;", Ct);
        }
        using var e = f.CreateAuthenticatedClient(f.Users.Editors[0]);
        await Problem(await Command(e, p.Id, "approve", Tag(p)), 503, "storage_unavailable");
        await using var verify = f.Services.CreateAsyncScope(); var storedDb = verify.ServiceProvider.GetRequiredService<BlogDbContext>();
        var stored = await storedDb.Posts.SingleAsync(Ct);
        Assert.Equal(p.Version, stored.Version); Assert.Equal(PostStatus.PendingReview, stored.Status); Assert.Null(stored.CurrentReviewId);
        Assert.Empty(await storedDb.ReviewDecisions.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task ActualEditInvalidatesApprovalAndPublishCannotUseOldDecision()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(PostStatus.PendingReview, f.Users.AuthorEditor.UserId); await Save(f, p);
        using var e = f.CreateAuthenticatedClient(f.Users.Editors[0]); using var o = f.CreateAuthenticatedClient(f.Users.AuthorEditor);
        var approved = await Command(e, p.Id, "approve", Tag(p)); Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var tag = approved.Headers.ETag!.ToString();
        var same = await Send(o, HttpMethod.Put, p.Id, tag, JsonSerializer.Serialize(new { title = p.Title, body = p.Body }));
        Assert.Equal(tag, same.Headers.ETag!.ToString()); Actions(await Json(same), "edit", "delete", "publish");
        var changed = await Send(o, HttpMethod.Put, p.Id, tag, "{\"title\":\"changed\",\"body\":\"changed\"}");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode); Actions(await Json(changed), "edit", "delete", "submit");
        await Problem(await Command(o, p.Id, "publish", changed.Headers.ETag!.ToString()), 404, "post_not_found");
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        var stored = await db.Posts.Include(x => x.Reviews).SingleAsync(Ct);
        Assert.Equal(PostStatus.Draft, stored.Status); Assert.False(stored.HasValidApproval()); Assert.Null(stored.Publication); Assert.Single(stored.Reviews);
    }
    private static string Tag(Post p) => '"' + p.Version.ToString() + '"';
    private static void Actions(JsonElement view, params string[] expected) => Assert.Equal(expected, view.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()));
    private static async Task Save(BlogApiFactory f, params Post[] posts)
    { await using var scope = f.Services.CreateAsyncScope(); await PostStates.SaveAsync(scope.ServiceProvider.GetRequiredService<BlogDbContext>(), posts); }
    private static async Task<HttpResponseMessage> Command(HttpClient client, Guid id, string action, string? tag, string? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/editor/posts/{id}/{action}");
        if (tag is not null) request.Headers.TryAddWithoutValidation("If-Match", tag);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.SendAsync(request, Ct);
    }
}

