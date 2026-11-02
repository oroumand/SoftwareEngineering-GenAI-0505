using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.ContentReview;

public sealed record PostContentReviewProjection(Guid PostId, string OwnerId, string Title, string Body,
    int ContentRevision, PostStatus Status, bool IsDeleted);
public sealed record ContentReviewSnapshot(Guid PostId, string OwnerId, string Title, string Body,
    int ContentRevision, PostStatus StatusAtStart, DateTimeOffset CapturedAt);
public sealed record ContentReviewInput(string Title, string Body);
public interface IPostContentReviewReader
{
    Task<PostContentReviewProjection?> ReadAsync(Guid id, CancellationToken cancellationToken);
}
public enum ContentReviewOutcome { Candidate, Refused, Unavailable, Timeout, InvalidOutput }
public sealed record ContentReviewGatewayResult(ContentReviewOutcome Outcome, ContentReviewReport? Report = null,
    string? ProviderModel = null);
public interface IContentReviewGateway
{
    Task<ContentReviewGatewayResult> ReviewAsync(ContentReviewInput input, CancellationToken cancellationToken);
}
