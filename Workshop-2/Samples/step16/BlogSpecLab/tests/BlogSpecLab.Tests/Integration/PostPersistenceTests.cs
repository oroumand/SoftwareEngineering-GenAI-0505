using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Core.Application.Persistence;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlogSpecLab.Tests.Integration;

public sealed class PostPersistenceTests
{
    [Fact]
    public async Task EmptySqliteMigrationHasRequiredMappingAndConditionalUpdateProtectsVersion()
    {
        await using var f = new BlogApiFactory(); await AuthorApiTests.Prepare(f);
        var ct = TestContext.Current.CancellationToken;
        var post = Post.Create(f.Users.Authors[0].UserId, "title", "body");
        await using (var setup = f.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<BlogDbContext>(); db.Add(post); await db.SaveChangesAsync(ct);
            var entity = db.Model.FindEntityType(typeof(Post))!;
            Assert.True(entity.FindProperty(nameof(Post.Version))!.IsConcurrencyToken);
            Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "OwnerId", "IsDeleted" }));
            Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Status", "IsDeleted" }));
        }
        await using var first = f.Services.CreateAsyncScope(); await using var second = f.Services.CreateAsyncScope();
        var firstDb = first.ServiceProvider.GetRequiredService<BlogDbContext>();
        var secondDb = second.ServiceProvider.GetRequiredService<BlogDbContext>();
        var one = await firstDb.Posts.SingleAsync(p => p.Id == post.Id, ct);
        var two = await secondDb.Posts.SingleAsync(p => p.Id == post.Id, ct);
        one.Edit("first", "body"); await firstDb.SaveChangesAsync(ct);
        two.Edit("second", "body");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync(ct));
        secondDb.ChangeTracker.Clear();
        Assert.Equal("first", (await secondDb.Posts.SingleAsync(p => p.Id == post.Id, ct)).Title);
    }

    [Fact]
    public async Task RollbackLeavesNoDraftAndSoftDeletionPreservesStoredHistory()
    {
        await using var f = new BlogApiFactory(); await AuthorApiTests.Prepare(f);
        var ct = TestContext.Current.CancellationToken;
        var post = PostStates.In(PostStatus.ChangesRequested, f.Users.Authors[0].UserId);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>(); await PostStates.SaveAsync(db, post);
            var session = scope.ServiceProvider.GetRequiredService<IWriteSession>(); Assert.True((await session.BeginAsync(ct)).IsSuccess);
            db.Add(Post.Create(post.OwnerId, "not committed", "body")); await session.RollbackAsync(ct);
        }
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var stored = await db.Posts.Include(p => p.Reviews).SingleAsync(ct);
            Assert.Equal(DateTimeKind.Utc, stored.Reviews.Single().DecidedAt.Kind);
            stored.Delete(); await db.SaveChangesAsync(ct);
        }
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            Assert.Empty(await db.Posts.ToArrayAsync(ct));
            Assert.True((await db.Posts.IgnoreQueryFilters().SingleAsync(ct)).IsDeleted);
            Assert.Single(await db.ReviewDecisions.ToArrayAsync(ct));
            var historical = await db.ReviewDecisions.SingleAsync(ct);
            db.Remove(historical);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
        }
    }
}
