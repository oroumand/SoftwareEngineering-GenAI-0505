using BlogSpecLab.Infra.DataAccess.Posts;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace BlogSpecLab.Tests.Integration;
public class ContentReviewReaderTests
{
    [Fact] public async Task Fresh_projection_does_not_track_or_include_history()
    {
        await using var f = new ContentReviewApiFactory(); var post = await f.Prepare();
        await using var db = Us3Support.Context(f); var reader = new PostContentReviewReader(db);
        Assert.Equal(post.Title, (await reader.ReadAsync(post.Id, TestContext.Current.CancellationToken))!.Title); Assert.Empty(db.ChangeTracker.Entries());
        await f.Change(post.Id, p => p.Edit("تازه", "متن تازه"));
        Assert.Equal("تازه", (await reader.ReadAsync(post.Id, TestContext.Current.CancellationToken))!.Title);
        await f.Change(post.Id, p => p.Delete()); Assert.True((await reader.ReadAsync(post.Id, TestContext.Current.CancellationToken))!.IsDeleted);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
