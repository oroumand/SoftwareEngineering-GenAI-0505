namespace BlogSpecLab.Application.Posts.Ports;

public interface IUnitOfWork
{
    Task<bool> CommitAsync(CancellationToken cancellationToken);
}
