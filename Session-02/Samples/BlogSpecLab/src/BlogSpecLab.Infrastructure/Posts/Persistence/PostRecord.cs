namespace BlogSpecLab.Infrastructure.Posts.Persistence;

public sealed class PostRecord
{
    public Guid Id { get; set; }

    public string AuthorId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? PublishedAt { get; set; }

    public string VersionToken { get; set; } = string.Empty;

    public uint Xmin { get; set; }
}
