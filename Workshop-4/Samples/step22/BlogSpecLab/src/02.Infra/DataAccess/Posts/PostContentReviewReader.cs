using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReview;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace BlogSpecLab.Infra.DataAccess.Posts;
public sealed class PostContentReviewReader(BlogDbContext db) : IPostContentReviewReader
{
    public async Task<PostContentReviewProjection?> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return await db.Posts.IgnoreQueryFilters().AsNoTracking().Where(p => p.Id == id)
                .Select(p => new PostContentReviewProjection(p.Id, p.OwnerId, p.Title, p.Body, p.ContentRevision, p.Status, p.IsDeleted))
                .SingleOrDefaultAsync(cancellationToken);
        }
        catch (SqliteException) { throw new StorageUnavailableException(); }
    }
}
