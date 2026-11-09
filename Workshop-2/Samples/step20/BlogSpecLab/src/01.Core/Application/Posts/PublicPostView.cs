namespace BlogSpecLab.Core.Application.Posts;

public sealed record PublicPostView(Guid Id, string Title, string Body, DateTime PublishedAt);
