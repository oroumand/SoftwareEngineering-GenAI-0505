namespace BlogSpecLab.Core.Domain.Posts;

public enum ReviewDecisionKind { Approve, RequestChanges }

public sealed class ReviewDecision
{
    public ReviewDecision(Guid id, Guid postId, int contentRevision, string reviewedTitle,
        string reviewedBody, ReviewDecisionKind decision, string reviewerId, DateTime decidedAt, string? reason)
    {
        if (id == Guid.Empty || postId == Guid.Empty || contentRevision < 1 || !Enum.IsDefined(decision)
            || decidedAt.Kind != DateTimeKind.Utc) throw new PostRuleException(PostRule.InvalidInput);
        Post.RequireText(reviewedTitle, "title"); Post.RequireText(reviewedBody, "body"); Post.RequireText(reviewerId, "reviewerId");
        if (decision == ReviewDecisionKind.RequestChanges) Post.RequireText(reason, "reason");
        Id = id; PostId = postId; ContentRevision = contentRevision; ReviewedTitle = reviewedTitle;
        ReviewedBody = reviewedBody; Decision = decision; ReviewerId = reviewerId; DecidedAt = decidedAt; Reason = reason;
    }
    public Guid Id { get; }
    public Guid PostId { get; }
    public int ContentRevision { get; }
    public string ReviewedTitle { get; }
    public string ReviewedBody { get; }
    public ReviewDecisionKind Decision { get; }
    public string ReviewerId { get; }
    public DateTime DecidedAt { get; }
    public string? Reason { get; }
}
