using BlogSpecLab.Application.Posts.Ports;
using BlogSpecLab.Domain.Posts.Entities;
using BlogSpecLab.Domain.Posts.ValueObjects;
using BlogSpecLab.Infrastructure.Posts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infrastructure.Posts.Repositories;

public sealed class EfPostRepository(BlogSpecLabDbContext dbContext) : IPostRepository
{
    private readonly Dictionary<Guid, PostRecord> _loadedRecords = [];

    public async Task<PostVersion> AddAsync(Post post, CancellationToken cancellationToken)
    {
        var version = new PostVersion(NewToken());
        var record = ToRecord(post, version.Value);
        dbContext.Posts.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<LoadedPost?> FindByIdAsync(PostId postId, CancellationToken cancellationToken)
    {
        var record = await dbContext.Posts.SingleOrDefaultAsync(item => item.Id == postId.Value, cancellationToken);
        if (record is null)
        {
            return null;
        }

        _loadedRecords[record.Id] = record;
        return new LoadedPost(ToAggregate(record), new PostVersion(record.VersionToken));
    }

    public async Task<SavePostResult> SaveAsync(Post post, PostVersion expectedVersion, CancellationToken cancellationToken)
    {
        if (!_loadedRecords.TryGetValue(post.Id.Value, out var record) ||
            !string.Equals(record.VersionToken, expectedVersion.Value, StringComparison.Ordinal))
        {
            return SavePostResult.PreconditionFailed();
        }

        record.Title = post.Title.Value;
        record.Content = post.Content.Value;
        record.Status = post.Status.ToString();
        record.PublishedAt = post.PublishedAt;
        var nextVersion = new PostVersion(NewToken());
        record.VersionToken = nextVersion.Value;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return SavePostResult.Saved(nextVersion);
        }
        catch (DbUpdateConcurrencyException)
        {
            return SavePostResult.PreconditionFailed();
        }
    }

    public async Task<IReadOnlyList<PublishedPostSummary>> ListPublishedAsync(CancellationToken cancellationToken) =>
        await dbContext.Posts
            .AsNoTracking()
            .Where(item => item.Status == nameof(PostStatus.Published))
            .OrderByDescending(item => item.PublishedAt)
            .Select(item => new PublishedPostSummary(new PostId(item.Id), item.Title, item.PublishedAt!.Value))
            .ToListAsync(cancellationToken);

    public async Task<PublishedPostDetail?> FindPublishedByIdAsync(PostId postId, CancellationToken cancellationToken) =>
        await dbContext.Posts
            .AsNoTracking()
            .Where(item => item.Id == postId.Value && item.Status == nameof(PostStatus.Published))
            .Select(item => new PublishedPostDetail(new PostId(item.Id), item.Title, item.Content, item.PublishedAt!.Value))
            .SingleOrDefaultAsync(cancellationToken);

    private static PostRecord ToRecord(Post post, string versionToken) => new()
    {
        Id = post.Id.Value,
        AuthorId = post.AuthorId.Value,
        Title = post.Title.Value,
        Content = post.Content.Value,
        Status = post.Status.ToString(),
        PublishedAt = post.PublishedAt,
        VersionToken = versionToken
    };

    private static Post ToAggregate(PostRecord record)
    {
        var post = Post.CreateDraft(
            new PostId(record.Id),
            new AuthorId(record.AuthorId),
            new PostTitle(record.Title),
            new PostContent(record.Content));

        if (record.Status == nameof(PostStatus.Published))
        {
            post.Publish(new AuthorId(record.AuthorId), record.PublishedAt!.Value);
        }

        return post;
    }

    private static string NewToken() => Guid.NewGuid().ToString("N");
}
