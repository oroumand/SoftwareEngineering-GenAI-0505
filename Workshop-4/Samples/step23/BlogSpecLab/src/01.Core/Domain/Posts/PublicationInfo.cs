namespace BlogSpecLab.Core.Domain.Posts;

public sealed class PublicationInfo
{
    public PublicationInfo(string publisherId, DateTime publishedAt, Guid approvedReviewId)
    {
        Post.RequireText(publisherId, "publisherId");
        if (publishedAt.Kind != DateTimeKind.Utc || approvedReviewId == Guid.Empty) throw new PostRuleException(PostRule.InvalidInput);
        PublisherId = publisherId; PublishedAt = publishedAt; ApprovedReviewId = approvedReviewId;
    }
    public string PublisherId { get; }
    public DateTime PublishedAt { get; }
    public Guid ApprovedReviewId { get; }
}
