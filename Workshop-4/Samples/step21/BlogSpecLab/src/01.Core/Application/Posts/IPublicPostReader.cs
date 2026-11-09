namespace BlogSpecLab.Core.Application.Posts;

public interface IPublicPostReader
{
    Task<IReadOnlyList<PublicPostView>> ListAsync(CancellationToken cancellationToken = default);
    Task<PublicPostView?> FindAsync(Guid id, CancellationToken cancellationToken = default);
}
