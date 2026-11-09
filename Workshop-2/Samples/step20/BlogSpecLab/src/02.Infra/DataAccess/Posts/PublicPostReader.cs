using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Domain.Posts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess.Posts;

public sealed class PublicPostReader(BlogDbContext db) : IPublicPostReader
{
    public async Task<IReadOnlyList<PublicPostView>> ListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await Projection().ToListAsync(cancellationToken);
            return posts.OrderBy(p => p.Id).ToArray();
        }
        catch (SqliteException) { throw new StorageUnavailableException(); }
    }

    public async Task<PublicPostView?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try { return await Projection(id).SingleOrDefaultAsync(cancellationToken); }
        catch (SqliteException) { throw new StorageUnavailableException(); }
    }

    // فقط ستون‌های قرارداد عمومی خوانده می‌شوند؛ تاریخچه و دادهٔ مدیریتی بارگذاری نمی‌شوند.
    private IQueryable<PublicPostView> Projection(Guid? id = null) => db.Posts.AsNoTracking()
        .Where(p => !p.IsDeleted && p.Status == PostStatus.Published && p.Publication != null)
        .Where(p => id == null || p.Id == id)
        .Select(p => new PublicPostView(p.Id, p.Title, p.Body, p.Publication!.PublishedAt));
}
