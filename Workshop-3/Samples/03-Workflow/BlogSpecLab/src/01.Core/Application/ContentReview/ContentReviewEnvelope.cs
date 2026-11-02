namespace BlogSpecLab.Core.Application.ContentReview;

public sealed record ContentReviewSource(int ContentRevision, DateTimeOffset CapturedAt);
public sealed record ContentReviewFreshness(DateTimeOffset CheckedAt, bool IsCurrentContent)
{
    public bool RequiresNewReview => !IsCurrentContent;
}
public sealed record ContentReviewEnvelope(Guid PostId, ContentReviewSource Source,
    ContentReviewFreshness Freshness, ContentReviewReport Report)
{
    public string SchemaVersion => "content_review_v1";
}
