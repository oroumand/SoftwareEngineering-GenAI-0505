using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Infra.DataAccess.Security;
using BlogSpecLab.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static BlogSpecLab.Tests.Integration.AuthorApiTests;
using static BlogSpecLab.Tests.Integration.Us3Support;

namespace BlogSpecLab.Tests.Integration;

public sealed class PublicationSecurityTests
{
    [Theory]
    [InlineData("missing", 401)] [InlineData("forged", 401)] [InlineData("expired", 401)]
    [InlineData("revoked", 401)] [InlineData("inactive", 403)] [InlineData("permission", 403)]
    public async Task CredentialAndCurrentPermissionMatrixRejectsWithoutLeakOrMutation(string kind, int status)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var user = f.Users.Editors[0]; var p = PostStates.In(PostStatus.Approved, f.Users.Authors[0].UserId); await Save(f, p);
        using var c = f.CreateAuthenticatedClient(user);
        if (kind == "missing") c.DefaultRequestHeaders.Authorization = null;
        if (kind == "forged") c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        await using (var db = Context(f))
        {
            if (kind == "expired") (await db.Credentials.SingleAsync(x => x.UserId == user.UserId, Ct)).ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            if (kind == "revoked") (await db.Credentials.SingleAsync(x => x.UserId == user.UserId, Ct)).RevokedAt = DateTime.UtcNow;
            if (kind == "inactive") (await db.UserAccess.SingleAsync(x => x.UserId == user.UserId, Ct)).IsActive = false;
            await db.SaveChangesAsync(Ct);
        }
        if (kind == "permission")
        {
            await using var scope = f.Services.CreateAsyncScope();
            Assert.True((await scope.ServiceProvider.GetRequiredService<AccessProvisioner>().RevokePermissionsAsync(user.UserId, Ct)).IsSuccess);
        }
        foreach (var action in new[] { "publish", "approve", "request-changes" })
        {
            var response = await Command(c, p, action, body: action == "request-changes" ? "{\"reason\":\"reason\"}" : null);
            await SafeProblem(response, status, status == 401 ? "unauthenticated" : "forbidden");
            if (status == 401) Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
            AssertUnchanged(p, await Stored(f, p.Id));
        }
    }
    [Theory]
    [InlineData(PostStatus.Draft, 404)] [InlineData(PostStatus.ChangesRequested, 404)]
    [InlineData(PostStatus.PendingReview, 409)]
    public async Task DirectPublishCannotBypassStateOrApproval(PostStatus status, int expected)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(status, f.Users.Authors[0].UserId); await Save(f, p);
        using var c = f.CreateAuthenticatedClient(f.Users.Editors[0]);
        await SafeProblem(await Command(c, p, "publish"), expected, expected == 404 ? "post_not_found" : "invalid_state");
        AssertUnchanged(p, await Stored(f, p.Id));
    }
    [Theory]
    [InlineData("old-content")] [InlineData("wrong-post")] [InlineData("self-review")]
    [InlineData("request-changes")] [InlineData("missing-review")]
    public async Task InvalidApprovalCannotPublish(string kind)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(PostStatus.Approved, f.Users.Authors[0].UserId);
        var review = p.Reviews.Single();
        switch (kind)
        {
            case "old-content": p.Edit("changed", "changed body"); typeof(Post).GetProperty(nameof(Post.Status))!.SetValue(p, PostStatus.Approved); break;
            case "missing-review": typeof(Post).GetProperty(nameof(Post.CurrentReviewId))!.SetValue(p, null); break;
        }
        await Save(f, p);
        // ارجاع به پست دیگر از fixture معتبر همان جدول، بدون نقض FK.
        if (kind == "wrong-post")
        {
            var other = Post.Create(f.Users.Authors[1].UserId, "other", "other body");
            await Save(f, other);
            await using var db = Context(f);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ReviewDecisions SET PostId = {other.Id} WHERE Id = {review.Id}", Ct);
        }
        if (kind is "self-review" or "request-changes")
        {
            await using var db = Context(f);
            if (kind == "self-review")
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ReviewDecisions SET ReviewerId = {p.OwnerId} WHERE Id = {review.Id}", Ct);
            else
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ReviewDecisions SET Decision = {"RequestChanges"}, Reason = {"reason"} WHERE Id = {review.Id}", Ct);
        }
        using var c = f.CreateAuthenticatedClient(f.Users.Editors[0]);
        await SafeProblem(await Command(c, p, "publish"), 409, "approval_required");
        // در fixture wrong-post تصمیم متعلق به Aggregate دیگری است و Include آن را نمی‌آورد.
        var actual = await Stored(f, p.Id); Assert.Equal(p.Version, actual.Version); Assert.Null(actual.Publication); Assert.Equal(PostStatus.Approved, actual.Status);
        Assert.Equal(p.Title, actual.Title); Assert.Equal(p.Body, actual.Body);
    }
    [Fact]
    public async Task ForgedHeadersAndBodiesDoNotCreatePermissionsOwnershipOrState()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(PostStatus.Approved, f.Users.Authors[0].UserId); await Save(f, p);
        using var a = f.CreateAuthenticatedClient(f.Users.Authors[1]); using var e = f.CreateAuthenticatedClient(f.Users.Editors[0]);
        a.DefaultRequestHeaders.Add("X-Role", "Editor"); a.DefaultRequestHeaders.Add("X-OwnerId", p.OwnerId);
        a.DefaultRequestHeaders.Add("X-Status", "Approved");
        await SafeProblem(await Command(a, p, "publish"), 403, "forbidden");
        await SafeProblem(await Send(a, HttpMethod.Put, p.Id, Tag(p), "{\"title\":\"changed\",\"body\":\"changed\"}"), 404, "post_not_found");
        foreach (var field in new[] { "role", "ownerId", "status", "reviewerId", "publisherId" })
        {
            await SafeProblem(await Command(e, p, "publish", body: JsonSerializer.Serialize(new Dictionary<string, string> { [field] = "forged" })), 400, "invalid_input");
            using var owner = f.CreateAuthenticatedClient(f.Users.Authors[0]);
            await SafeProblem(await Send(owner, HttpMethod.Put, p.Id, Tag(p), JsonSerializer.Serialize(new Dictionary<string, string> { [field] = "forged", ["title"] = "changed", ["body"] = "changed" })), 400, "invalid_input");
            AssertUnchanged(p, await Stored(f, p.Id));
        }
    }
    [Fact]
    public async Task SelfApprovalIsForbiddenForDualRoleOwnerWithoutMutation()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(PostStatus.PendingReview, f.Users.AuthorEditor.UserId); await Save(f, p);
        using var c = f.CreateAuthenticatedClient(f.Users.AuthorEditor);
        await SafeProblem(await Command(c, p, "approve"), 403, "self_approval_forbidden");
        AssertUnchanged(p, await Stored(f, p.Id));
    }
    [Theory]
    [InlineData("owner")] [InlineData("editor")] [InlineData("dual")]
    public async Task PublishedIsImmutableForEveryRole(string role)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var owner = role == "dual" ? f.Users.AuthorEditor : f.Users.Authors[0];
        var p = PostStates.In(PostStatus.Published, owner.UserId); await Save(f, p);
        using var c = f.CreateAuthenticatedClient(role == "editor" ? f.Users.Editors[0] : owner);
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete })
        {
            await SafeProblem(await Send(c, method, p.Id, Tag(p), method == HttpMethod.Put ? "{\"title\":\"changed\",\"body\":\"changed\"}" : null),
                role == "editor" ? 403 : 409, role == "editor" ? "forbidden" : "published_post_immutable");
            AssertUnchanged(p, await Stored(f, p.Id));
        }
    }
    internal static async Task SafeProblem(HttpResponseMessage response, int status, string code)
    {
        await Problem(response, status, code); var error = await Json(response);
        foreach (var name in new[] { "body", "ownerId", "publication", "lastReview", "allowedActions", "reviewedBody", "titleContent" })
            Assert.False(error.TryGetProperty(name, out _));
        if (status is not (409 or 412))
        { Assert.False(error.TryGetProperty("currentStatus", out _)); Assert.False(error.TryGetProperty("currentVersion", out _)); }
    }
}
