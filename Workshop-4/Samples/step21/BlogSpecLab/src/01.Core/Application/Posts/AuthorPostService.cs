using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Persistence;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.Posts;

public sealed class AuthorPostService(IPostStore store, IUserAccessReader accessReader, IWriteSession session)
{
    public Task<OperationResult<PostView>> CreateAsync(string? userId, string? title, string? body, CancellationToken ct = default) =>
        WriteAsync(userId, access =>
        {
            var post = Post.Create(access.UserId, title, body); store.Add(post);
            return Task.FromResult(OperationResult<PostView>.Success(PostView.From(post, access)));
        }, ct);

    public async Task<OperationResult<IReadOnlyList<PostView>>> ListAsync(string? userId, CancellationToken ct = default)
    {
        try
        {
            var access = await RequireAccessAsync(userId, ct);
            if (!access.IsSuccess) return OperationResult<IReadOnlyList<PostView>>.Fail(access.Failure!);
            var posts = await store.ListOwnedAsync(access.Value!.UserId, ct);
            return OperationResult<IReadOnlyList<PostView>>.Success(posts.Where(p => !p.IsDeleted && p.OwnerId == access.Value.UserId)
                .OrderBy(p => p.Id).Select(p => PostView.From(p, access.Value)).ToArray());
        }
        catch (StorageUnavailableException) { return OperationResult<IReadOnlyList<PostView>>.Fail(OperationError.StorageUnavailable); }
    }

    public async Task<OperationResult<PostView>> GetAsync(string? userId, Guid id, CancellationToken ct = default)
    {
        try
        {
            var access = await RequireAccessAsync(userId, ct);
            if (!access.IsSuccess) return OperationResult<PostView>.Fail(access.Failure!);
            var post = await FindOwnedAsync(access.Value!, id, ct);
            return post is null ? OperationResult<PostView>.Fail(OperationError.PostNotFound)
                : OperationResult<PostView>.Success(PostView.From(post, access.Value!));
        }
        catch (StorageUnavailableException) { return OperationResult<PostView>.Fail(OperationError.StorageUnavailable); }
    }

    public Task<OperationResult<PostView>> EditAsync(string? userId, Guid id, Guid version, string? title, string? body, CancellationToken ct = default) =>
        MutateAsync(userId, id, version, p => p.Edit(title, body), (p, a) => PostView.From(p, a), ct);
    public Task<OperationResult<Unit>> DeleteAsync(string? userId, Guid id, Guid version, CancellationToken ct = default) =>
        MutateAsync(userId, id, version, p => p.Delete(), (_, _) => default(Unit), ct);
    public Task<OperationResult<PostView>> SubmitAsync(string? userId, Guid id, Guid version, CancellationToken ct = default) =>
        MutateAsync(userId, id, version, p => p.Submit(), (p, a) => PostView.From(p, a), ct);

    private async Task<OperationResult<T>> MutateAsync<T>(string? userId, Guid id, Guid version, Action<Post> change,
        Func<Post, UserAccess, T> view, CancellationToken ct)
    {
        var result = await WriteAsync(userId, async access =>
        {
            var post = await FindOwnedAsync(access, id, ct);
            if (post is null) return OperationResult<T>.Fail(OperationError.PostNotFound);
            if (post.Version != version) return OperationResult<T>.Fail(new OperationFailure(OperationError.StaleVersion,
                CurrentStatus: post.Status.ToString(), CurrentVersion: post.Version));
            try { change(post); }
            catch (PostRuleException error) { return OperationResult<T>.Fail(RuleFailure(error, post)); }
            return OperationResult<T>.Success(view(post, access));
        }, ct);
        if (result.Failure is not { Error: OperationError.StaleVersion, CurrentVersion: null }) return result;
        // فقط گزارش تعارض Commit پس از rollback؛ فرمان تجاری دوباره اجرا نمی‌شود.
        var current = await GetAsync(userId, id, ct);
        return current.IsSuccess ? OperationResult<T>.Fail(new OperationFailure(OperationError.StaleVersion,
            CurrentStatus: current.Value!.Status, CurrentVersion: current.Value.Version)) : OperationResult<T>.Fail(current.Failure!);
    }

    private async Task<OperationResult<T>> WriteAsync<T>(string? userId, Func<UserAccess, Task<OperationResult<T>>> execute, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return OperationResult<T>.Fail(OperationError.Unauthenticated);
        var begin = await session.BeginAsync(ct);
        if (!begin.IsSuccess) return OperationResult<T>.Fail(begin.Failure!);
        var committed = false;
        try
        {
            var access = await RequireAccessAsync(userId, ct);
            if (!access.IsSuccess) return OperationResult<T>.Fail(access.Failure!);
            var result = await execute(access.Value!);
            if (!result.IsSuccess) return result;
            var commit = await session.CommitAsync(ct);
            if (!commit.IsSuccess) return OperationResult<T>.Fail(commit.Failure!);
            committed = true;
            return result;
        }
        catch (PostRuleException error) { return OperationResult<T>.Fail(RuleFailure(error)); }
        catch (StorageUnavailableException) { return OperationResult<T>.Fail(OperationError.StorageUnavailable); }
        finally { if (!committed) await session.RollbackAsync(CancellationToken.None); }
    }

    private async Task<OperationResult<UserAccess>> RequireAccessAsync(string? userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return OperationResult<UserAccess>.Fail(OperationError.Unauthenticated);
        var access = await accessReader.ReadAsync(userId, ct);
        return access is { CanWrite: true, IsActive: true } && access.UserId == userId
            ? OperationResult<UserAccess>.Success(access) : OperationResult<UserAccess>.Fail(OperationError.Forbidden);
    }
    private async Task<Post?> FindOwnedAsync(UserAccess access, Guid id, CancellationToken ct)
    {
        var post = await store.FindAsync(id, ct);
        return post is not null && !post.IsDeleted && post.OwnerId == access.UserId ? post : null;
    }
    private static OperationFailure RuleFailure(PostRuleException error, Post? post = null)
    {
        var code = error.Rule switch {
            PostRule.InvalidInput => OperationError.InvalidInput, PostRule.InvalidState => OperationError.InvalidState,
            PostRule.PublishedPostImmutable => OperationError.PublishedPostImmutable, _ => OperationError.PostNotFound };
        var errors = error.Field is null ? null : new Dictionary<string, string[]> { [error.Field] = ["مقدار غیرخالی لازم است."] };
        return new(code, errors, code is OperationError.InvalidState or OperationError.PublishedPostImmutable ? post?.Status.ToString() : null,
            code is OperationError.InvalidState or OperationError.PublishedPostImmutable ? post?.Version : null);
    }
}
