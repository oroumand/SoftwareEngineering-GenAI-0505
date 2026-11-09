using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Persistence;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Core.Domain.Posts;
namespace BlogSpecLab.Core.Application.Posts;

public sealed class EditorPostService(IPostStore store, IUserAccessReader accessReader, IWriteSession session, TimeProvider clock)
{
    public async Task<OperationResult<IReadOnlyList<PostView>>> ListAsync(string? userId, CancellationToken ct = default)
    {
        try
        {
            var access = await RequireAccessAsync(userId, false, ct);
            if (!access.IsSuccess) return OperationResult<IReadOnlyList<PostView>>.Fail(access.Failure!);
            var posts = await store.ListEditorQueueAsync(ct);
            return OperationResult<IReadOnlyList<PostView>>.Success(posts.Where(IsReviewable).OrderBy(p => p.Id)
                .Select(p => PostView.From(p, access.Value!)).ToArray());
        }
        catch (StorageUnavailableException) { return OperationResult<IReadOnlyList<PostView>>.Fail(OperationError.StorageUnavailable); }
    }
    public async Task<OperationResult<PostView>> GetAsync(string? userId, Guid id, CancellationToken ct = default)
    {
        try
        {
            var access = await RequireAccessAsync(userId, false, ct);
            if (!access.IsSuccess) return OperationResult<PostView>.Fail(access.Failure!);
            var post = await store.FindAsync(id, ct);
            return post is not null && IsReviewable(post) ? OperationResult<PostView>.Success(PostView.From(post, access.Value!))
                : OperationResult<PostView>.Fail(OperationError.PostNotFound);
        }
        catch (StorageUnavailableException) { return OperationResult<PostView>.Fail(OperationError.StorageUnavailable); }
    }
    public Task<OperationResult<PostView>> RequestChangesAsync(string? userId, Guid id, Guid version, string? reason, CancellationToken ct = default) =>
        MutateAsync(userId, id, version, EditorAction.RequestChanges, reason, ct);
    public Task<OperationResult<PostView>> ApproveAsync(string? userId, Guid id, Guid version, CancellationToken ct = default) =>
        MutateAsync(userId, id, version, EditorAction.Approve, null, ct);
    public Task<OperationResult<PostView>> PublishAsync(string? userId, Guid id, Guid version, CancellationToken ct = default) =>
        MutateAsync(userId, id, version, EditorAction.Publish, null, ct);

    private async Task<OperationResult<PostView>> MutateAsync(string? userId, Guid id, Guid version, EditorAction action, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return OperationResult<PostView>.Fail(OperationError.Unauthenticated);
        var begin = await session.BeginAsync(ct);
        if (!begin.IsSuccess) return OperationResult<PostView>.Fail(begin.Failure!);
        var committed = false;
        OperationResult<PostView> result;
        try { result = await ExecuteAsync(); }
        catch (StorageUnavailableException) { result = OperationResult<PostView>.Fail(OperationError.StorageUnavailable); }
        finally { if (!committed) await session.RollbackAsync(CancellationToken.None); }
        // پس از rollback فقط بازخوانی؛ استثنای Publish فرمان تجاری را دوباره اجرا نمی‌کند.
        if (result.Failure is { Error: OperationError.StaleVersion, CurrentVersion: null })
        {
            try
            {
                var access = await RequireAccessAsync(userId, action == EditorAction.Publish, ct);
                if (!access.IsSuccess) return OperationResult<PostView>.Fail(access.Failure!);
                var current = await store.FindAsync(id, ct);
                if (!CanAccess(current, action)) return OperationResult<PostView>.Fail(OperationError.PostNotFound);
                if (action == EditorAction.Approve && current!.OwnerId == userId) return OperationResult<PostView>.Fail(OperationError.SelfApprovalForbidden);
                if (action == EditorAction.Publish && current!.HasValidPublication())
                    return OperationResult<PostView>.Success(PostView.From(current, access.Value!));
                return OperationResult<PostView>.Fail(Conflict(OperationError.StaleVersion, current!));
            }
            catch (StorageUnavailableException) { return OperationResult<PostView>.Fail(OperationError.StorageUnavailable); }
        }
        return result;

        async Task<OperationResult<PostView>> ExecuteAsync()
        {
            var access = await RequireAccessAsync(userId, action == EditorAction.Publish, ct);
            if (!access.IsSuccess) return OperationResult<PostView>.Fail(access.Failure!);
            var post = await store.FindAsync(id, ct);
            if (!CanAccess(post, action)) return OperationResult<PostView>.Fail(OperationError.PostNotFound);
            if (action == EditorAction.Approve && post!.OwnerId == access.Value!.UserId)
                return OperationResult<PostView>.Fail(OperationError.SelfApprovalForbidden);
            // انتشار نخست پس از مجوز جاری، مستقل از Version قدیمی بازگردانده می‌شود؛ بدون Commit.
            if (action == EditorAction.Publish && post!.HasValidPublication())
                return OperationResult<PostView>.Success(PostView.From(post, access.Value!));
            if (post!.Version != version) return OperationResult<PostView>.Fail(Conflict(OperationError.StaleVersion, post));
            var now = clock.GetUtcNow().UtcDateTime;
            try
            {
                switch (action)
                {
                    case EditorAction.RequestChanges: post.RequestChanges(access.Value!.UserId, now, reason); break;
                    case EditorAction.Approve: post.Approve(access.Value!.UserId, now); break;
                    case EditorAction.Publish: post.Publish(access.Value!.UserId, now); break;
                }
            }
            catch (PostRuleException error) { return OperationResult<PostView>.Fail(RuleFailure(error, post)); }
            if (action != EditorAction.Publish) store.AddReview(post.Reviews.Single(r => r.Id == post.CurrentReviewId));
            var commit = await session.CommitAsync(ct);
            if (!commit.IsSuccess) return OperationResult<PostView>.Fail(commit.Failure!);
            committed = true;
            return OperationResult<PostView>.Success(PostView.From(post, access.Value!));
        }
    }
    private async Task<OperationResult<UserAccess>> RequireAccessAsync(string? userId, bool publishing, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return OperationResult<UserAccess>.Fail(OperationError.Unauthenticated);
        var access = await accessReader.ReadAsync(userId, ct);
        return access is { IsActive: true } && access.UserId == userId && (publishing ? access.CanPublish : access.CanReview)
            ? OperationResult<UserAccess>.Success(access) : OperationResult<UserAccess>.Fail(OperationError.Forbidden);
    }
    private static bool IsReviewable(Post post) => !post.IsDeleted && post.Status is PostStatus.PendingReview or PostStatus.Approved;
    private static bool CanAccess(Post? post, EditorAction action) => post is not null && !post.IsDeleted
        && (IsReviewable(post) || action == EditorAction.Publish && post.Status == PostStatus.Published);
    private static OperationFailure Conflict(OperationError error, Post post) => new(error, CurrentStatus: post.Status.ToString(), CurrentVersion: post.Version);
    private static OperationFailure RuleFailure(PostRuleException error, Post post)
    {
        var code = error.Rule switch {
            PostRule.InvalidInput => OperationError.InvalidInput, PostRule.InvalidState => OperationError.InvalidState,
            PostRule.ApprovalRequired => OperationError.ApprovalRequired, PostRule.SelfApprovalForbidden => OperationError.SelfApprovalForbidden,
            PostRule.PublishedPostImmutable => OperationError.PublishedPostImmutable, _ => OperationError.PostNotFound };
        if (code == OperationError.InvalidInput)
            return new(code, error.Field is null ? null : new Dictionary<string, string[]> { [error.Field] = ["مقدار غیرخالی لازم است."] });
        return code is OperationError.InvalidState or OperationError.ApprovalRequired or OperationError.PublishedPostImmutable
            ? Conflict(code, post) : new(code);
    }
    private enum EditorAction { RequestChanges, Approve, Publish }
}
