using System.Net;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Persistence;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Infra.DataAccess.Persistence;
using BlogSpecLab.Infra.DataAccess.Posts;
using BlogSpecLab.Infra.DataAccess.Security;
using BlogSpecLab.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static BlogSpecLab.Tests.Integration.AuthorApiTests;
using static BlogSpecLab.Tests.Integration.Us3Support;

namespace BlogSpecLab.Tests.Integration;

public sealed class PublicationAtomicityTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PublishCommitConflictRereadsPublicationAndCurrentPermissionWithoutBusinessRetry(bool revokeAfterConflict)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(PostStatus.Approved, f.Users.Authors[0].UserId); await Save(f, p);
        await using var staleDb = Context(f);
        // مسیر ذخیرهٔ مشروط نیز باید حتی با Aggregate قدیمی rollback و بازخوانی کند.
        var staleStore = new PostStore(staleDb); Assert.NotNull(await staleStore.FindAsync(p.Id, Ct));
        using var winner = f.CreateAuthenticatedClient(f.Users.Editors[0]);
        var published = await Command(winner, p, "publish"); Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var snapshot = await Stored(f, p.Id);
        await using var session = new ConflictSession(new SqliteWriteSession(staleDb), async () =>
        {
            if (!revokeAfterConflict) return;
            await using var revokeDb = Context(f); await using var revokeSession = new SqliteWriteSession(revokeDb);
            Assert.True((await new AccessProvisioner(revokeDb, revokeSession, TimeProvider.System)
                .RevokePermissionsAsync(f.Users.Editors[1].UserId, Ct)).IsSuccess);
        });
        var result = await new EditorPostService(staleStore, new UserAccessReader(staleDb), session, TimeProvider.System)
            .PublishAsync(f.Users.Editors[1].UserId, p.Id, p.Version, Ct);
        Assert.Equal(1, session.Attempts);
        if (revokeAfterConflict) Assert.Equal(OperationError.Forbidden, result.Failure!.Error);
        else
        {
            Assert.True(result.IsSuccess); Assert.Equal(snapshot.Version, result.Value!.Version);
            Assert.Equal(snapshot.Publication!.PublisherId, result.Value.Publication!.PublisherId);
            Assert.Equal(snapshot.Publication.PublishedAt, result.Value.Publication.PublishedAt);
        }
        AssertUnchanged(snapshot, await Stored(f, p.Id));
    }
    [Fact]
    public async Task ConcurrentPublishReturnsFirstPublicationWithoutSecondCommitEvenWithOldVersion()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(PostStatus.Approved, f.Users.Authors[0].UserId); await Save(f, p);
        await using var db1 = Context(f); await using var db2 = Context(f);
        await using var first = new ObservedSession(new SqliteWriteSession(db1), pause: true);
        await using var second = new ObservedSession(new SqliteWriteSession(db2));
        var a = Task.Run(() => Execute(db1, first, p, "publish", p.OwnerId, f.Users.Editors[0].UserId), Ct);
        await first.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        var b = Task.Run(() => Execute(db2, second, p, "publish", p.OwnerId, f.Users.Editors[1].UserId), Ct);
        try { await second.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct); Assert.False(second.Acquired.Task.IsCompleted); }
        finally { first.Release.TrySetResult(); }
        var winner = await a.WaitAsync(TimeSpan.FromSeconds(15), Ct); var loser = await b.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        Assert.True(winner.IsSuccess); Assert.True(loser.IsSuccess);
        Assert.Equal(winner.Value!.Version, loser.Value!.Version); Assert.Equal(winner.Value.Publication, loser.Value.Publication);
        Assert.Equal(1, first.Commits); Assert.Equal(0, second.Commits);
        var stored = await Stored(f, p.Id); Assert.True(stored.HasValidPublication()); Assert.Single(stored.Reviews);
        Assert.Equal(f.Users.Editors[0].UserId, stored.Publication!.PublisherId);
    }
    [Fact]
    public async Task StalePublishIsRejectedBeforeFirstPublicationButAcceptedAfterwardsOnlyWithCurrentPermissionAndValidHeader()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(PostStatus.Approved, f.Users.Authors[0].UserId); await Save(f, p);
        using var c = f.CreateAuthenticatedClient(f.Users.Editors[0]);
        var stale = '"' + Guid.NewGuid().ToString() + '"';
        await Problem(await Command(c, p, "publish", stale), 412, "stale_version"); AssertUnchanged(p, await Stored(f, p.Id));
        var first = await Command(c, p, "publish"); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var again = await Command(c, p, "publish", stale); Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal((await Json(first)).GetRawText(), (await Json(again)).GetRawText());
        Assert.Empty((await Json(again)).GetProperty("allowedActions").EnumerateArray());
        var snapshot = await Stored(f, p.Id);
        await Problem(await Command(c, p, "publish", "*"), 400, "invalid_input");
        using (var missing = new HttpRequestMessage(HttpMethod.Post, $"/api/editor/posts/{p.Id}/publish"))
            await Problem(await c.SendAsync(missing, Ct), 428, "version_required");
        await using (var db = Context(f))
        await using (var session = new SqliteWriteSession(db))
            Assert.True((await new AccessProvisioner(db, session, TimeProvider.System).RevokePermissionsAsync(f.Users.Editors[0].UserId, Ct)).IsSuccess);
        await Problem(await Command(c, p, "publish", stale), 403, "forbidden");
        AssertUnchanged(snapshot, await Stored(f, p.Id));
    }
    [Theory]
    [InlineData(true, "publish")] [InlineData(false, "publish")]
    [InlineData(true, "approve")] [InlineData(false, "approve")]
    public async Task OperatorPermissionRevocationAndSensitiveOperationAreSerializedInBothOrders(bool revokeFirst, string action)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(action == "publish" ? PostStatus.Approved : PostStatus.PendingReview, f.Users.Authors[0].UserId); await Save(f, p);
        var user = f.Users.Editors[0].UserId;
        await using var db1 = Context(f); await using var db2 = Context(f);
        await using var first = new ObservedSession(new SqliteWriteSession(db1), pause: true);
        await using var second = new ObservedSession(new SqliteWriteSession(db2));
        async Task<OperationResult<Unit>> Revoke(BlogDbContext db, IWriteSession session) =>
            await new AccessProvisioner(db, session, TimeProvider.System).RevokePermissionsAsync(user, Ct);
        var revoke = revokeFirst ? Task.Run(() => Revoke(db1, first), Ct) : null;
        var operation = revokeFirst ? null : Task.Run(() => Execute(db1, first, p, action, p.OwnerId, user), Ct);
        await first.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        revoke ??= Task.Run(() => Revoke(db2, second), Ct);
        operation ??= Task.Run(() => Execute(db2, second, p, action, p.OwnerId, user), Ct);
        try { await second.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct); Assert.False(second.Acquired.Task.IsCompleted); }
        finally { first.Release.TrySetResult(); }
        Assert.True((await revoke.WaitAsync(TimeSpan.FromSeconds(15), Ct)).IsSuccess);
        var result = await operation.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        var stored = await Stored(f, p.Id);
        if (revokeFirst) { Assert.Equal(OperationError.Forbidden, result.Failure!.Error); AssertUnchanged(p, stored); }
        else { Assert.True(result.IsSuccess); Assert.Equal(action == "publish" ? PostStatus.Published : PostStatus.Approved, stored.Status); Assert.True(stored.HasValidApproval()); }
        await using var verify = Context(f); var access = await new UserAccessReader(verify).ReadAsync(user, Ct);
        Assert.False(access!.CanPublish); Assert.False(access.CanReview);
    }
    [Fact]
    public async Task PersistentWriteLockReturnsHttp503WithoutPartialChanges()
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(PostStatus.Approved, f.Users.Authors[0].UserId); await Save(f, p);
        using var c = f.CreateAuthenticatedClient(f.Users.Editors[0]);
        await using var blocker = Context(f); await using var session = new SqliteWriteSession(blocker);
        Assert.True((await session.BeginAsync(Ct)).IsSuccess);
        var response = await Task.Run(() => Command(c, p, "publish"), Ct);
        await Problem(response, 503, "storage_unavailable");
        await session.RollbackAsync(Ct); AssertUnchanged(p, await Stored(f, p.Id));
    }
    [Theory]
    [InlineData("approve")] [InlineData("publish")]
    public async Task RealCommitFailureAfterSuccessfulSaveRollsBackPostReviewPublicationAndInjectedRow(string action)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(action == "approve" ? PostStatus.PendingReview : PostStatus.Approved, f.Users.Authors[0].UserId); await Save(f, p);
        await using var db = Context(f); await using var session = new ObservedSession(new SqliteWriteSession(db));
        var reader = new DeferredFailureReader(db);
        var service = new EditorPostService(new PostStore(db), reader, session, TimeProvider.System);
        var result = action == "approve" ? await service.ApproveAsync(f.Users.Editors[0].UserId, p.Id, p.Version, Ct)
            : await service.PublishAsync(f.Users.Editors[0].UserId, p.Id, p.Version, Ct);
        Assert.Equal(OperationError.StorageUnavailable, result.Failure!.Error); Assert.Equal(1, session.Commits);
        Assert.True(reader.Injected); Assert.Empty(db.ChangeTracker.Entries());
        AssertUnchanged(p, await Stored(f, p.Id));
        await using var verify = Context(f);
        Assert.False(await verify.Credentials.AnyAsync(x => x.Id == reader.InjectedId, Ct));
        Assert.Equal(p.Reviews.Count, await verify.ReviewDecisions.CountAsync(Ct));
    }
    [Theory]
    [InlineData("approve")] [InlineData("publish")]
    public async Task StorageStatementFailureReturnsHttp503AndRollsBackAllChanges(string action)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(action == "approve" ? PostStatus.PendingReview : PostStatus.Approved, f.Users.Authors[0].UserId); await Save(f, p);
        await using (var db = Context(f))
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_post BEFORE UPDATE ON Posts BEGIN SELECT RAISE(ABORT, 'test failure'); END;", Ct);
        using var c = f.CreateAuthenticatedClient(f.Users.Editors[0]);
        await Problem(await Command(c, p, action), 503, "storage_unavailable"); AssertUnchanged(p, await Stored(f, p.Id));
        await using var verify = Context(f); Assert.Equal(p.Reviews.Count, await verify.ReviewDecisions.CountAsync(Ct));
    }
    private sealed class DeferredFailureReader(BlogDbContext db) : IUserAccessReader
    {
        internal Guid InjectedId { get; } = Guid.NewGuid();
        internal bool Injected { get; private set; }
        public async Task<UserAccess?> ReadAsync(string userId, CancellationToken cancellationToken = default)
        {
            var access = await new UserAccessReader(db).ReadAsync(userId, cancellationToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA defer_foreign_keys = ON;", cancellationToken);
            // خطا فقط در COMMIT رخ می‌دهد؛ INSERT و SaveChanges پست موفق هستند.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Credentials (Id, UserId, TokenHash) VALUES ({InjectedId}, {Guid.NewGuid().ToString()}, {Guid.NewGuid().ToString()})", cancellationToken);
            Injected = true; return access;
        }
    }
    private sealed class ConflictSession(IWriteSession inner, Func<Task> afterConflict) : IWriteSession
    {
        internal int Attempts { get; private set; }
        public Task<OperationResult<Unit>> BeginAsync(CancellationToken cancellationToken = default) => inner.BeginAsync(cancellationToken);
        public async Task<OperationResult<Unit>> CommitAsync(CancellationToken cancellationToken = default)
        {
            Attempts++; var result = await inner.CommitAsync(cancellationToken);
            Assert.Equal(OperationError.StaleVersion, result.Failure!.Error);
            await afterConflict(); return result;
        }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => inner.RollbackAsync(cancellationToken);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
