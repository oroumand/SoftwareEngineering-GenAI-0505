using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Domain.Posts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess.Posts;

public sealed class PostStore(BlogDbContext db) : IPostStore
{
    public async Task<Post?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try { return await db.Posts.Include(x => x.Reviews).SingleOrDefaultAsync(x => x.Id == id, cancellationToken); }
        catch (SqliteException) { throw new StorageUnavailableException(); }
    }
    public async Task<IReadOnlyList<Post>> ListOwnedAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await db.Posts.AsNoTracking().Include(x => x.Reviews).Where(x => x.OwnerId == userId).ToListAsync(cancellationToken);
            return posts.OrderBy(x => x.Id).ToArray();
        }
        catch (SqliteException) { throw new StorageUnavailableException(); }
    }
    public void Add(Post post) => db.Posts.Add(post);
}
