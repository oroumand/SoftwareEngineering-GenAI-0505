using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Persistence;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Tests.Fixtures;
using Xunit;
namespace BlogSpecLab.Tests.Application;
public sealed class EditorPostTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task QueueOnlyContainsReviewablePostsSorted()
    {
        var f = new Dependencies(); foreach (var status in Enum.GetValues<PostStatus>()) f.Posts.Add(PostStates.In(status));
        var deleted = PostStates.In(PostStatus.PendingReview); deleted.Delete(); f.Posts.Add(deleted);
        var list = await f.Service.ListAsync("editor", Ct); Assert.True(list.IsSuccess);
        Assert.Equal(f.Posts.Where(p => !p.IsDeleted && p.Status is PostStatus.PendingReview or PostStatus.Approved).OrderBy(p => p.Id).Select(p => p.Id), list.Value!.Select(p => p.Id));
        foreach (var p in f.Posts) Assert.Equal(!p.IsDeleted && p.Status is PostStatus.PendingReview or PostStatus.Approved, (await f.Service.GetAsync("editor", p.Id, Ct)).IsSuccess);
    }
    [Fact]
    public async Task PermissionsAreIndependentCurrentAndReadInTransactionWithoutHistoricalRecheck()
    {
        var f = new Dependencies(); var p = PostStates.In(PostStatus.PendingReview); f.Posts.Add(p);
        Assert.Contains("approve", (await f.Service.GetAsync("editor", p.Id, Ct)).Value!.AllowedActions);
        f.Access = new("editor", false, false, true, true);
        Assert.Equal(OperationError.Forbidden, (await f.Service.ApproveAsync("editor", p.Id, p.Version, Ct)).Failure!.Error);
        Assert.Equal(new[] { "begin", "access:editor", "rollback" }, f.Calls.TakeLast(3)); Assert.Empty(p.Reviews);
        f.Access = new("editor", false, true, false, true);
        Assert.True((await f.Service.ApproveAsync("editor", p.Id, p.Version, Ct)).IsSuccess);
        Assert.Equal(OperationError.Forbidden, (await f.Service.PublishAsync("editor", p.Id, p.Version, Ct)).Failure!.Error);
        f.Access = new("publisher", false, false, true, true); f.Calls.Clear();
        Assert.True((await f.Service.PublishAsync("publisher", p.Id, p.Version, Ct)).IsSuccess);
        Assert.Equal(new[] { "begin", "access:publisher", "commit" }, f.Calls); Assert.Equal("editor", p.Reviews.Single().ReviewerId);
    }
    [Fact]
    public async Task OwnerCanReturnButCannotApproveAndCanPublishAnotherReviewersApproval()
    {
        var f = new Dependencies { Access = new("author", true, true, true, true) }; var p = PostStates.In(PostStatus.PendingReview); f.Posts.Add(p);
        Assert.DoesNotContain("approve", (await f.Service.GetAsync("author", p.Id, Ct)).Value!.AllowedActions);
        Assert.Equal(OperationError.SelfApprovalForbidden, (await f.Service.ApproveAsync("author", p.Id, Guid.NewGuid(), Ct)).Failure!.Error);
        Assert.True((await f.Service.RequestChangesAsync("author", p.Id, p.Version, "reason", Ct)).IsSuccess);
        p.Submit(); f.Access = new("editor", false, true, false, true); Assert.True((await f.Service.ApproveAsync("editor", p.Id, p.Version, Ct)).IsSuccess);
        f.Access = new("author", true, false, true, true); Assert.True((await f.Service.PublishAsync("author", p.Id, p.Version, Ct)).IsSuccess);
        Assert.Equal("author", p.Publication!.PublisherId); Assert.Equal("author", p.OwnerId); Assert.False(p.IsDeleted); Assert.Equal("عنوان", p.Title);
    }
    [Theory]
    [InlineData(null, OperationError.Unauthenticated)] [InlineData("missing", OperationError.Forbidden)] [InlineData("inactive", OperationError.Forbidden)]
    public async Task IdentityAndActiveAccountRequired(string? user, OperationError error)
    {
        var f = new Dependencies(); var p = PostStates.In(PostStatus.PendingReview); f.Posts.Add(p);
        Assert.Equal(error, (await f.Service.ListAsync(user, Ct)).Failure!.Error); Assert.Equal(error, (await f.Service.GetAsync(user, p.Id, Ct)).Failure!.Error);
        Assert.Equal(error, (await f.Service.ApproveAsync(user, p.Id, p.Version, Ct)).Failure!.Error);
        Assert.Equal(error, (await f.Service.RequestChangesAsync(user, p.Id, p.Version, "reason", Ct)).Failure!.Error);
        Assert.Equal(error, (await f.Service.PublishAsync(user, p.Id, p.Version, Ct)).Failure!.Error); Assert.Equal(0, f.Commits); Assert.Empty(p.Reviews);
    }
    [Fact]
    public async Task StaleInputScopeAndAuthorAccessFailWithoutMutation()
    {
        var f = new Dependencies(); var p = PostStates.In(PostStatus.PendingReview); f.Posts.Add(p);
        Assert.Equal(OperationError.StaleVersion, (await f.Service.ApproveAsync("editor", p.Id, Guid.NewGuid(), Ct)).Failure!.Error);
        Assert.Equal(OperationError.InvalidInput, (await f.Service.RequestChangesAsync("editor", p.Id, p.Version, " ", Ct)).Failure!.Error);
        var draft = PostStates.In(PostStatus.Draft); f.Posts.Add(draft); var result = await f.Service.ApproveAsync("editor", draft.Id, Guid.NewGuid(), Ct);
        Assert.Equal(OperationError.PostNotFound, result.Failure!.Error); Assert.Null(result.Failure.CurrentVersion); Assert.Empty(p.Reviews); Assert.Equal(0, f.Commits);
        var author = new AuthorPostService(f, f, f);
        Assert.Equal(OperationError.Forbidden, (await author.EditAsync("editor", p.Id, p.Version, "new", "new", Ct)).Failure!.Error);
        Assert.Equal(OperationError.Forbidden, (await author.DeleteAsync("editor", p.Id, p.Version, Ct)).Failure!.Error);
        p.Approve("reviewer", DateTime.UtcNow); p.Edit("changed", p.Body);
        Assert.False((await f.Service.PublishAsync("editor", p.Id, p.Version, Ct)).IsSuccess); Assert.Null(p.Publication);
    }
    [Fact]
    public async Task RepeatPublicationHasNoCommitAndCurrentPublisherPermissionIsStillRequired()
    {
        var f = new Dependencies(); var p = PostStates.In(PostStatus.PendingReview); f.Posts.Add(p);
        Assert.True((await f.Service.ApproveAsync("editor", p.Id, p.Version, Ct)).IsSuccess);
        Assert.True((await f.Service.PublishAsync("editor", p.Id, p.Version, Ct)).IsSuccess);
        var publication = p.Publication; var version = p.Version; f.Calls.Clear(); var commits = f.Commits;
        var repeat = await f.Service.PublishAsync("editor", p.Id, version, Ct);
        Assert.True(repeat.IsSuccess); Assert.Empty(repeat.Value!.AllowedActions);
        Assert.Equal(commits, f.Commits); Assert.DoesNotContain("commit", f.Calls); Assert.Same(publication, p.Publication);
        f.Access = new("editor", false, true, false, true);
        Assert.Equal(OperationError.Forbidden, (await f.Service.PublishAsync("editor", p.Id, version, Ct)).Failure!.Error);
        Assert.Equal(version, p.Version); Assert.Same(publication, p.Publication);
    }
    private sealed class Dependencies : IPostStore, IUserAccessReader, IWriteSession
    {
        public List<Post> Posts = []; public List<string> Calls = []; public int Commits;
        public UserAccess Access = new("editor", false, true, true, true);
        public EditorPostService Service => new(this, this, this, TimeProvider.System);
        public Task<UserAccess?> ReadAsync(string userId, CancellationToken cancellationToken = default)
        { Calls.Add("access:" + userId); return Task.FromResult<UserAccess?>(userId == "inactive" ? new(userId, false, true, true, false) : userId == Access.UserId ? Access : null); }
        public Task<Post?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Posts.SingleOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<Post>> ListOwnedAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Post>>(Posts.Where(p => p.OwnerId == userId).ToArray());
        public Task<IReadOnlyList<Post>> ListEditorQueueAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Post>>(Posts.ToArray());
        public void Add(Post post) => Posts.Add(post);
        public void AddReview(ReviewDecision decision) { }
        public Task<OperationResult<Unit>> BeginAsync(CancellationToken cancellationToken = default) { Calls.Add("begin"); return Task.FromResult(OperationResult<Unit>.Success(default)); }
        public Task<OperationResult<Unit>> CommitAsync(CancellationToken cancellationToken = default) { Calls.Add("commit"); Commits++; return Task.FromResult(OperationResult<Unit>.Success(default)); }
        public Task RollbackAsync(CancellationToken cancellationToken = default) { Calls.Add("rollback"); return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
