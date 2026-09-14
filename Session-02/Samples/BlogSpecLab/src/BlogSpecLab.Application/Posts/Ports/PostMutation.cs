using BlogSpecLab.Domain.Posts.Entities;
using BlogSpecLab.Domain.Posts.ValueObjects;

namespace BlogSpecLab.Application.Posts.Ports;

public sealed record LoadedPost(Post Post, PostVersion Version);

public sealed record SavePostResult(bool Succeeded, PostVersion? NewVersion)
{
    public static SavePostResult PreconditionFailed() => new(false, null);

    public static SavePostResult Saved(PostVersion newVersion) => new(true, newVersion);
}

public sealed record PublishedPostSummary(PostId Id, string Title, DateTimeOffset PublishedAt);

public sealed record PublishedPostDetail(PostId Id, string Title, string Content, DateTimeOffset PublishedAt);
