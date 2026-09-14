using BlogSpecLab.Application.Posts.Ports;
using BlogSpecLab.Infrastructure.Posts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infrastructure.Posts.Repositories;

public sealed class EfUnitOfWork(BlogSpecLabDbContext dbContext) : IUnitOfWork
{
    public async Task<bool> CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
