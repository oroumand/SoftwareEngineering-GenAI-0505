namespace BlogSpecLab.Api.Posts.Models;

public sealed record PublishedPostSummaryResponse(Guid Id, string Title, DateTimeOffset PublishedAt);

public sealed record PublishedPostDetailResponse(Guid Id, string Title, string Content, DateTimeOffset PublishedAt);
