namespace BlogSpecLab.Core.Domain.Posts;

public sealed class Post
{
    private readonly List<ReviewDecision> reviews = [];
    private Post() { }
    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Body { get; private set; } = "";
    public PostStatus Status { get; private set; }
    public int ContentRevision { get; private set; }
    public Guid Version { get; private set; }
    public bool IsDeleted { get; private set; }
    public Guid? CurrentReviewId { get; private set; }
    public PublicationInfo? Publication { get; private set; }
    public IReadOnlyCollection<ReviewDecision> Reviews => reviews.AsReadOnly();
    public static Post Create(string ownerId, string? title, string? body)
    {
        RequireText(ownerId, "ownerId"); ValidateContent(title, body);
        return new() { Id = Guid.NewGuid(), OwnerId = ownerId, Title = title!, Body = body!,
            Status = PostStatus.Draft, ContentRevision = 1, Version = Guid.NewGuid() };
    }
    public void Edit(string? title, string? body)
    {
        RequireMutable(); ValidateContent(title, body);
        if (Title == title && Body == body) return;
        var revision = checked(ContentRevision + 1);
        Title = title!; Body = body!; ContentRevision = revision;
        if (Status is PostStatus.PendingReview or PostStatus.Approved) Status = PostStatus.Draft;
        // آخرین تصمیم برای نمایش تاریخچه حفظ می‌شود؛ تطبیق محتوا اعتبار تأیید را تعیین می‌کند.
        Version = Guid.NewGuid();
    }
    public void Delete() { RequireMutable(); IsDeleted = true; Version = Guid.NewGuid(); }
    public void Submit()
    {
        RequireMutable();
        if (Status is not (PostStatus.Draft or PostStatus.ChangesRequested)) throw new PostRuleException(PostRule.InvalidState);
        ValidateContent(Title, Body); Status = PostStatus.PendingReview; Version = Guid.NewGuid();
    }
    private void RequireMutable()
    {
        if (IsDeleted) throw new PostRuleException(PostRule.PostNotFound);
        if (Status == PostStatus.Published) throw new PostRuleException(PostRule.PublishedPostImmutable);
    }
    private static void ValidateContent(string? title, string? body) { RequireText(title, "title"); RequireText(body, "body"); }
    internal static void RequireText(string? text, string field)
    { if (string.IsNullOrWhiteSpace(text)) throw new PostRuleException(PostRule.InvalidInput, field); }
    public bool HasValidApproval()
    {
        var decision = reviews.SingleOrDefault(x => x.Id == CurrentReviewId);
        return decision is { Decision: ReviewDecisionKind.Approve } && decision.PostId == Id
            && decision.ReviewerId != OwnerId && decision.ContentRevision == ContentRevision
            && decision.ReviewedTitle == Title && decision.ReviewedBody == Body;
    }
    public void RequestChanges(string reviewerId, DateTime decidedAt, string? reason)
    {
        RequirePendingReview();
        var decision = NewDecision(reviewerId, decidedAt, ReviewDecisionKind.RequestChanges, reason);
        RecordDecision(decision, PostStatus.ChangesRequested);
    }
    public void Approve(string reviewerId, DateTime decidedAt)
    {
        RequirePendingReview();
        if (reviewerId == OwnerId) throw new PostRuleException(PostRule.SelfApprovalForbidden);
        var decision = NewDecision(reviewerId, decidedAt, ReviewDecisionKind.Approve, null);
        RecordDecision(decision, PostStatus.Approved);
    }
    public bool HasValidPublication() => Status == PostStatus.Published && !IsDeleted
        && Publication is not null && Publication.ApprovedReviewId == CurrentReviewId && HasValidApproval();
    public void Publish(string publisherId, DateTime publishedAt)
    {
        if (IsDeleted) throw new PostRuleException(PostRule.PostNotFound);
        if (HasValidPublication()) return;
        if (Status != PostStatus.Approved) throw new PostRuleException(PostRule.InvalidState);
        if (!HasValidApproval()) throw new PostRuleException(PostRule.ApprovalRequired);
        var publication = new PublicationInfo(publisherId, publishedAt, CurrentReviewId!.Value);
        Publication = publication; Status = PostStatus.Published; Version = Guid.NewGuid();
    }
    private void RequirePendingReview()
    {
        RequireMutable();
        if (Status != PostStatus.PendingReview) throw new PostRuleException(PostRule.InvalidState);
    }
    private ReviewDecision NewDecision(string reviewerId, DateTime decidedAt, ReviewDecisionKind kind, string? reason) =>
        new(Guid.NewGuid(), Id, ContentRevision, Title, Body, kind, reviewerId, decidedAt, reason);
    private void RecordDecision(ReviewDecision decision, PostStatus status)
    {
        reviews.Add(decision); CurrentReviewId = decision.Id; Status = status; Version = Guid.NewGuid();
    }
}

