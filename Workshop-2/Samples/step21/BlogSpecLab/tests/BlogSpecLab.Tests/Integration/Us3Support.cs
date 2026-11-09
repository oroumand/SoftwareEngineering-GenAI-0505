using System.Text;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Persistence;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Infra.DataAccess.Persistence;
using BlogSpecLab.Infra.DataAccess.Posts;
using BlogSpecLab.Infra.DataAccess.Security;
using BlogSpecLab.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlogSpecLab.Tests.Integration;

internal static class Us3Support
{
    internal static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static string Tag(Post post) => '"' + post.Version.ToString() + '"';
    internal static async Task Save(BlogApiFactory factory, params Post[] posts)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await PostStates.SaveAsync(scope.ServiceProvider.GetRequiredService<BlogDbContext>(), posts);
    }
    internal static BlogDbContext Context(BlogApiFactory factory) => new(new DbContextOptionsBuilder<BlogDbContext>()
        .UseSqlite($"Data Source={factory.DatabasePath};Pooling=False;Default Timeout=1", o => o.CommandTimeout(1)).Options);
    internal static async Task<Post> Stored(BlogApiFactory factory, Guid id)
    {
        await using var db = Context(factory);
        return await db.Posts.IgnoreQueryFilters().Include(p => p.Reviews).SingleAsync(p => p.Id == id, Ct);
    }
    internal static async Task<HttpResponseMessage> Command(HttpClient client, Post post, string action,
        string? tag = null, string? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/editor/posts/{post.Id}/{action}");
        request.Headers.TryAddWithoutValidation("If-Match", tag ?? Tag(post));
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.SendAsync(request, Ct);
    }
    internal static Task<OperationResult<PostView>> Execute(BlogDbContext db, IWriteSession session, Post initial,
        string action, string author, string editor)
    {
        var store = new PostStore(db); var reader = new UserAccessReader(db);
        if (action == "edit") return new AuthorPostService(store, reader, session)
            .EditAsync(author, initial.Id, initial.Version, "changed", "changed body", Ct);
        if (action == "delete") return Delete();
        var service = new EditorPostService(store, reader, session, TimeProvider.System);
        return action switch
        {
            "approve" => service.ApproveAsync(editor, initial.Id, initial.Version, Ct),
            "return" => service.RequestChangesAsync(editor, initial.Id, initial.Version, "reason", Ct),
            _ => service.PublishAsync(editor, initial.Id, initial.Version, Ct)
        };
        async Task<OperationResult<PostView>> Delete()
        {
            var result = await new AuthorPostService(store, reader, session).DeleteAsync(author, initial.Id, initial.Version, Ct);
            return result.IsSuccess ? OperationResult<PostView>.Success(null!) : OperationResult<PostView>.Fail(result.Failure!);
        }
    }
    internal static void Apply(Post post, string action, string editor)
    {
        switch (action)
        {
            case "edit": post.Edit("changed", "changed body"); break;
            case "delete": post.Delete(); break;
            case "approve": post.Approve(editor, DateTime.UtcNow); break;
            case "return": post.RequestChanges(editor, DateTime.UtcNow, "reason"); break;
            default: post.Publish(editor, DateTime.UtcNow); break;
        }
    }
    internal static void AssertUnchanged(Post initial, Post actual)
    {
        Assert.Equal(initial.Version, actual.Version); Assert.Equal(initial.Status, actual.Status);
        Assert.Equal(initial.Title, actual.Title); Assert.Equal(initial.Body, actual.Body);
        Assert.Equal(initial.OwnerId, actual.OwnerId); Assert.Equal(initial.IsDeleted, actual.IsDeleted);
        Assert.Equal(initial.ContentRevision, actual.ContentRevision);
        Assert.Equal(initial.CurrentReviewId, actual.CurrentReviewId);
        Assert.Equal(initial.Publication?.PublisherId, actual.Publication?.PublisherId);
        Assert.Equal(initial.Publication?.PublishedAt, actual.Publication?.PublishedAt);
        Assert.Equal(initial.Publication?.ApprovedReviewId, actual.Publication?.ApprovedReviewId);
        Assert.Equal(initial.Reviews.Select(r => r.Id), actual.Reviews.Select(r => r.Id));
    }
}

// هماهنگی فقط در آزمون؛ Begin/Commit واقعی و قفل SQLite حفظ می‌شوند.
internal sealed class ObservedSession(IWriteSession inner, bool pause = false) : IWriteSession
{
    internal TaskCompletionSource Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Acquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int Commits { get; private set; }
    public async Task<OperationResult<Unit>> BeginAsync(CancellationToken cancellationToken = default)
    {
        Attempted.TrySetResult();
        var result = await inner.BeginAsync(cancellationToken);
        if (result.IsSuccess)
        {
            Acquired.TrySetResult();
            if (pause) await Release.Task.WaitAsync(cancellationToken);
        }
        return result;
    }
    public Task<OperationResult<Unit>> CommitAsync(CancellationToken cancellationToken = default)
    { Commits++; return inner.CommitAsync(cancellationToken); }
    public Task RollbackAsync(CancellationToken cancellationToken = default) => inner.RollbackAsync(cancellationToken);
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
