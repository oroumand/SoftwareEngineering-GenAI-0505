using BlogSpecLab.Core.Application.Common;

namespace BlogSpecLab.Core.Application.Posts;

public sealed class PublicPostService(IPublicPostReader reader)
{
    public async Task<OperationResult<IReadOnlyList<PublicPostView>>> ListAsync(CancellationToken ct = default)
    {
        try { return OperationResult<IReadOnlyList<PublicPostView>>.Success(await reader.ListAsync(ct)); }
        catch (StorageUnavailableException) { return OperationResult<IReadOnlyList<PublicPostView>>.Fail(OperationError.StorageUnavailable); }
    }

    public async Task<OperationResult<PublicPostView>> GetAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var post = await reader.FindAsync(id, ct);
            return post is null ? OperationResult<PublicPostView>.Fail(OperationError.PostNotFound)
                : OperationResult<PublicPostView>.Success(post);
        }
        catch (StorageUnavailableException) { return OperationResult<PublicPostView>.Fail(OperationError.StorageUnavailable); }
    }
}
