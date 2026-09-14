using BlogSpecLab.Domain.Posts.Errors;
using BlogSpecLab.Domain.Posts.ValueObjects;

namespace BlogSpecLab.Domain.Posts.Entities;

public sealed class Post
{
    private Post(PostId id, AuthorId authorId, PostTitle title, PostContent content)
    {
        Id = id;
        AuthorId = authorId;
        Title = title;
        Content = content;
        Status = PostStatus.Draft;
    }

    public PostId Id { get; }

    public AuthorId AuthorId { get; }

    public PostTitle Title { get; private set; }

    public PostContent Content { get; private set; }

    public PostStatus Status { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public static Post CreateDraft(PostId id, AuthorId authorId, PostTitle title, PostContent content)
    {
        if (id.Value == Guid.Empty)
        {
            throw new PostValidationException("شناسهٔ پست الزامی است.");
        }

        ArgumentNullException.ThrowIfNull(authorId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(content);

        return new Post(id, authorId, title, content);
    }

    public void EditDraft(AuthorId actorAuthorId, PostTitle title, PostContent content)
    {
        EnsureOwner(actorAuthorId);
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(content);

        Title = title;
        Content = content;
    }

    public void Publish(AuthorId actorAuthorId, DateTimeOffset publishedAt)
    {
        EnsureOwner(actorAuthorId);
        EnsureDraft();

        Status = PostStatus.Published;
        PublishedAt = publishedAt;
    }

    private void EnsureOwner(AuthorId actorAuthorId)
    {
        ArgumentNullException.ThrowIfNull(actorAuthorId);

        if (AuthorId != actorAuthorId)
        {
            throw new PostOwnershipException("فقط مالک پست می‌تواند آن را تغییر دهد.");
        }
    }

    private void EnsureDraft()
    {
        if (Status != PostStatus.Draft)
        {
            throw new PostStateException("پست منتشرشده قابل تغییر نیست.");
        }
    }
}
