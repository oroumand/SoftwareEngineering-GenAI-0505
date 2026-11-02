using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Infra.DataAccess.Persistence;
using BlogSpecLab.Infra.DataAccess.Posts;
using BlogSpecLab.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static BlogSpecLab.Tests.Integration.AuthorApiTests;
using static BlogSpecLab.Tests.Integration.Us3Support;

namespace BlogSpecLab.Tests.Integration;

public sealed class PostConcurrencyTests
{
    public static TheoryData<string, string, PostStatus> Orders => new()
    {
        { "edit", "approve", PostStatus.PendingReview }, { "approve", "edit", PostStatus.PendingReview },
        { "edit", "publish", PostStatus.Approved }, { "publish", "edit", PostStatus.Approved },
        { "delete", "publish", PostStatus.Approved }, { "publish", "delete", PostStatus.Approved },
        { "approve", "return", PostStatus.PendingReview }, { "return", "approve", PostStatus.PendingReview }
    };
    [Theory]
    [MemberData(nameof(Orders))]
    public async Task ConditionalUpdateRejectsStaleTrackedAggregateAndRollsBackWholeDecision(string first, string second, PostStatus status)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(status, f.Users.Authors[0].UserId); await Save(f, p);
        await using var db1 = Context(f); await using var db2 = Context(f);
        Assert.NotSame(db1.Database.GetDbConnection(), db2.Database.GetDbConnection());
        var store1 = new PostStore(db1); var store2 = new PostStore(db2);
        var a = (await store1.FindAsync(p.Id, Ct))!; var b = (await store2.FindAsync(p.Id, Ct))!;
        Assert.Equal(a.Version, b.Version);
        await using var session1 = new SqliteWriteSession(db1); await using var session2 = new SqliteWriteSession(db2);
        Assert.True((await session1.BeginAsync(Ct)).IsSuccess); Apply(a, first, f.Users.Editors[0].UserId);
        if (first is "approve" or "return") store1.AddReview(a.Reviews.Single(r => r.Id == a.CurrentReviewId));
        Assert.True((await session1.CommitAsync(Ct)).IsSuccess);
        Assert.True((await session2.BeginAsync(Ct)).IsSuccess); Apply(b, second, f.Users.Editors[1].UserId);
        if (second is "approve" or "return") store2.AddReview(b.Reviews.Single(r => r.Id == b.CurrentReviewId));
        var loser = await session2.CommitAsync(Ct); Assert.Equal(OperationError.StaleVersion, loser.Failure!.Error);
        Assert.Empty(db2.ChangeTracker.Entries());
        AssertUnchanged(a, await Stored(f, p.Id));
        await AssertHistory(f, p, first);
    }
    [Theory]
    [MemberData(nameof(Orders))]
    public async Task RealWriteLockSerializesApplicationCommandsInBothOrders(string first, string second, PostStatus status)
    {
        await using var f = new BlogApiFactory(); await Prepare(f);
        var p = PostStates.In(status, f.Users.Authors[0].UserId); await Save(f, p);
        await using var db1 = Context(f); await using var db2 = Context(f);
        var v1 = await db1.Posts.AsNoTracking().Select(x => x.Version).SingleAsync(Ct);
        var v2 = await db2.Posts.AsNoTracking().Select(x => x.Version).SingleAsync(Ct); Assert.Equal(v1, v2);
        await using var winnerSession = new ObservedSession(new SqliteWriteSession(db1), pause: true);
        await using var loserSession = new ObservedSession(new SqliteWriteSession(db2));
        var winner = Task.Run(() => Execute(db1, winnerSession, p, first, p.OwnerId, f.Users.Editors[0].UserId), Ct);
        await winnerSession.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        var loser = Task.Run(() => Execute(db2, loserSession, p, second, p.OwnerId, f.Users.Editors[1].UserId), Ct);
        try
        {
            await loserSession.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
            Assert.False(loserSession.Acquired.Task.IsCompleted);
        }
        finally { winnerSession.Release.TrySetResult(); }
        Assert.True((await winner.WaitAsync(TimeSpan.FromSeconds(15), Ct)).IsSuccess);
        var result = await loser.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        var inaccessible = first is "edit" or "delete" && second is "approve" or "publish" || first == "return";
        Assert.Equal(inaccessible ? OperationError.PostNotFound : OperationError.StaleVersion, result.Failure!.Error);
        Assert.Equal(1, winnerSession.Commits); Assert.Equal(0, loserSession.Commits);
        var stored = await Stored(f, p.Id);
        Assert.NotEqual(p.Version, stored.Version);
        if (first == "edit") { Assert.Equal(PostStatus.Draft, stored.Status); Assert.False(stored.HasValidApproval()); }
        if (first == "delete") Assert.True(stored.IsDeleted);
        if (first == "publish") Assert.True(stored.HasValidPublication());
        if (first == "approve") Assert.True(stored.HasValidApproval());
        await AssertHistory(f, p, first);
    }
    private static async Task AssertHistory(BlogApiFactory f, Post initial, string winner)
    {
        await using var db = Context(f); var actual = await Stored(f, initial.Id);
        var reviews = await db.ReviewDecisions.ToListAsync(Ct);
        Assert.Equal(initial.Reviews.Count + (winner is "approve" or "return" ? 1 : 0), reviews.Count);
        Assert.All(reviews, r => Assert.Equal(initial.Id, r.PostId));
        Assert.Equal(reviews.Select(r => r.Id).Order(), actual.Reviews.Select(r => r.Id).Order());
        if (actual.Publication is { } publication)
            Assert.Contains(reviews, r => r.Id == publication.ApprovedReviewId && r.Decision == ReviewDecisionKind.Approve);
        else Assert.NotEqual(PostStatus.Published, actual.Status);
    }
}
