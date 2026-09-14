using BlogSpecLab.Domain.Posts.Entities;
using BlogSpecLab.Domain.Posts.ValueObjects;

namespace BlogSpecLab.Application.Posts.Ports;

public interface IPostRepository
{
    Task<PostVersion> AddAsync(Post post, CancellationToken cancellationToken);

    Task<LoadedPost?> FindByIdAsync(PostId postId, CancellationToken cancellationToken);

    Task<SavePostResult> SaveAsync(Post post, PostVersion expectedVersion, CancellationToken cancellationToken);

    Task<IReadOnlyList<PublishedPostSummary>> ListPublishedAsync(CancellationToken cancellationToken);

    Task<PublishedPostDetail?> FindPublishedByIdAsync(PostId postId, CancellationToken cancellationToken);
}
