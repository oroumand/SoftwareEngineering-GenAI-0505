using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.Posts;

public interface IPostStore
{
    Task<Post?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Post>> ListOwnedAsync(string userId, CancellationToken cancellationToken = default);
    void Add(Post post);
}
