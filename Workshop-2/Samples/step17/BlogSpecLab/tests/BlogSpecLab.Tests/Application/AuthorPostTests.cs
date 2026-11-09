using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Persistence;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Tests.Fixtures;
using Xunit;

namespace BlogSpecLab.Tests.Application;

public sealed class AuthorPostTests
{
    [Fact]
    public async Task CreateUsesCurrentIdentityAndTransactionAndAllSixUseCasesWork()
    {
        var fake = new MemoryDependencies();
        var service = fake.Service;
        var created = await service.CreateAsync("author", "title", "body", TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess);
        var view = created.Value!;
        Assert.Equal("author", view.OwnerId);
        Assert.Equal(new[] { "begin", "access", "add", "commit" }, fake.Calls);
        fake.Calls.Clear();
        Assert.Single((await service.ListAsync("author", TestContext.Current.CancellationToken)).Value!);
        Assert.True((await service.GetAsync("author", view.Id, TestContext.Current.CancellationToken)).IsSuccess);
        var edited = await service.EditAsync("author", view.Id, view.Version, "new", "body", TestContext.Current.CancellationToken);
        Assert.True(edited.IsSuccess);
        var submitted = await service.SubmitAsync("author", view.Id, edited.Value!.Version, TestContext.Current.CancellationToken);
        Assert.Equal("PendingReview", submitted.Value!.Status);
        Assert.True((await service.DeleteAsync("author", view.Id, submitted.Value.Version, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal(OperationError.PostNotFound, (await service.GetAsync("author", view.Id, TestContext.Current.CancellationToken)).Failure!.Error);
    }

    [Theory]
    [InlineData(null, OperationError.Unauthenticated)] [InlineData(" ", OperationError.Unauthenticated)]
    [InlineData("missing", OperationError.Forbidden)] [InlineData("inactive", OperationError.Forbidden)]
    [InlineData("editor", OperationError.Forbidden)]
    public async Task EveryUseCaseRequiresValidIdentityAndCurrentWritePermission(string? user, OperationError error)
    {
        var f = new MemoryDependencies();
        var p = Post.Create("author", "title", "body"); f.Posts.Add(p);
        Assert.Equal(error, (await f.Service.CreateAsync(user, "title", "body", TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(error, (await f.Service.ListAsync(user, TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(error, (await f.Service.GetAsync(user, p.Id, TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(error, (await f.Service.EditAsync(user, p.Id, p.Version, "new", "body", TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(error, (await f.Service.DeleteAsync(user, p.Id, p.Version, TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(error, (await f.Service.SubmitAsync(user, p.Id, p.Version, TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(0, f.Commits);
        Assert.Equal("title", p.Title);
    }

    [Fact]
    public async Task OwnershipAndDeletionAreCheckedBeforeVersionOrStateAndNoContentEscapes()
    {
        var f = new MemoryDependencies();
        var p = PostStates.In(PostStatus.Published); f.Posts.Add(p);
        Assert.Equal(OperationError.PostNotFound, (await f.Service.GetAsync("other", p.Id, TestContext.Current.CancellationToken)).Failure!.Error);
        foreach (var result in new[] {
            await f.Service.EditAsync("other", p.Id, Guid.NewGuid(), "new", "body", TestContext.Current.CancellationToken),
            await f.Service.SubmitAsync("other", p.Id, Guid.NewGuid(), TestContext.Current.CancellationToken) })
        {
            Assert.Equal(OperationError.PostNotFound, result.Failure!.Error);
            Assert.Null(result.Failure.CurrentStatus); Assert.Null(result.Failure.CurrentVersion);
        }
        Assert.Equal(OperationError.PostNotFound, (await f.Service.DeleteAsync("other", p.Id, Guid.NewGuid(), TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Empty((await f.Service.ListAsync("other", TestContext.Current.CancellationToken)).Value!);
        Assert.Equal(OperationError.PostNotFound, (await f.Service.GetAsync("author", Guid.NewGuid(), TestContext.Current.CancellationToken)).Failure!.Error);
        var deleted = Post.Create("author", "title", "body"); deleted.Delete(); f.Posts.Add(deleted);
        Assert.Equal(OperationError.PostNotFound, (await f.Service.SubmitAsync("author", deleted.Id, deleted.Version, TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(0, f.Commits);
    }

    [Fact]
    public async Task StaleAndInvalidInputAndStateFailWithoutChangingPost()
    {
        var f = new MemoryDependencies();
        var p = PostStates.In(PostStatus.PendingReview); f.Posts.Add(p); var version = p.Version;
        Assert.Equal(OperationError.StaleVersion, (await f.Service.EditAsync("author", p.Id, Guid.NewGuid(), "new", "body", TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(OperationError.StaleVersion, (await f.Service.DeleteAsync("author", p.Id, Guid.NewGuid(), TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(OperationError.StaleVersion, (await f.Service.SubmitAsync("author", p.Id, Guid.NewGuid(), TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(OperationError.InvalidInput, (await f.Service.EditAsync("author", p.Id, version, " ", "body", TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(OperationError.InvalidState, (await f.Service.SubmitAsync("author", p.Id, version, TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(version, p.Version); Assert.Equal(0, f.Commits);
    }

    [Fact]
    public async Task PermissionIsReadAgainAfterPriorViewAndInsideWriteSession()
    {
        var f = new MemoryDependencies(); var p = Post.Create("author", "title", "body"); f.Posts.Add(p);
        Assert.Contains("edit", (await f.Service.GetAsync("author", p.Id, TestContext.Current.CancellationToken)).Value!.AllowedActions);
        f.Access = new("author", false, false, false, true); f.Calls.Clear();
        Assert.Equal(OperationError.Forbidden, (await f.Service.EditAsync("author", p.Id, p.Version, "new", "body", TestContext.Current.CancellationToken)).Failure!.Error);
        Assert.Equal(new[] { "begin", "access", "rollback" }, f.Calls);
        Assert.Equal("title", p.Title);
    }

    [Theory]
    [InlineData(OperationError.StaleVersion)] [InlineData(OperationError.StorageUnavailable)]
    public async Task CommitFailureRollsBackAndIsNotRetried(OperationError error)
    {
        var f = new MemoryDependencies { CommitError = error };
        var p = Post.Create("author", "title", "body"); f.Posts.Add(p); var version = p.Version;
        var result = await f.Service.EditAsync("author", p.Id, version, "new", "body", TestContext.Current.CancellationToken);
        Assert.Equal(error, result.Failure!.Error);
        Assert.Equal("title", f.Posts.Single().Title);
        Assert.Equal(1, f.Calls.Count(x => x == "begin"));
        Assert.Equal(1, f.Calls.Count(x => x == "commit"));
    }

    private sealed class MemoryDependencies : IPostStore, IUserAccessReader, IWriteSession
    {
        public List<Post> Posts = [];
        public List<string> Calls = [];
        private List<Post> snapshot = [];
        public int Commits;
        public OperationError? CommitError;
        public UserAccess Access = new("author", true, false, false, true);
        public AuthorPostService Service => new(this, this, this);
        public Task<UserAccess?> ReadAsync(string userId, CancellationToken cancellationToken = default)
        {
            Calls.Add("access");
            return Task.FromResult<UserAccess?>(userId switch {
                "author" => Access, "other" => new("other", true, false, false, true),
                "inactive" => new("inactive", true, false, false, false),
                "editor" => new("editor", false, true, true, true), _ => null });
        }
        public Task<Post?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Posts.SingleOrDefault(p => p.Id == id && !p.IsDeleted));
        public Task<IReadOnlyList<Post>> ListOwnedAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Post>>(Posts.Where(p => p.OwnerId == userId && !p.IsDeleted).OrderBy(p => p.Id).ToArray());
        public void Add(Post post) { Calls.Add("add"); Posts.Add(post); }
        public void AddReview(ReviewDecision decision) { }
        public Task<IReadOnlyList<Post>> ListEditorQueueAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Post>>(Posts.Where(p => !p.IsDeleted && p.Status is PostStatus.PendingReview or PostStatus.Approved).ToArray());
        public Task<OperationResult<Unit>> BeginAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("begin"); snapshot = Posts.Select(Clone).ToList();
            return Task.FromResult(OperationResult<Unit>.Success(default));
        }
        public Task<OperationResult<Unit>> CommitAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("commit");
            if (CommitError is { } error) return Task.FromResult(OperationResult<Unit>.Fail(error));
            Commits++; snapshot = Posts.Select(Clone).ToList();
            return Task.FromResult(OperationResult<Unit>.Success(default));
        }
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        { Calls.Add("rollback"); Posts = snapshot.Select(Clone).ToList(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        private static Post Clone(Post p) => (Post)typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(p, null)!;
    }
}
