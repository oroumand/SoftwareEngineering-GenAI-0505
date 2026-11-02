using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess.Security;

public sealed class UserAccessReader(BlogDbContext db) : IUserAccessReader
{
    public async Task<UserAccess?> ReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var row = await db.UserAccess.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            return row is null ? null : new(row.UserId, row.CanWrite, row.CanReview, row.CanPublish, row.IsActive);
        }
        catch (SqliteException) { throw new StorageUnavailableException(); }
    }
}
