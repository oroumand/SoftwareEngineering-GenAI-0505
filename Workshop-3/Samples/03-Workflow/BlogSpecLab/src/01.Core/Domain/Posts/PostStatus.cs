namespace BlogSpecLab.Core.Domain.Posts;

public enum PostStatus { Draft, PendingReview, ChangesRequested, Approved, Published }
public enum PostRule { InvalidInput, InvalidState, PublishedPostImmutable, PostNotFound, SelfApprovalForbidden, ApprovalRequired }
public sealed class PostRuleException(PostRule rule, string? field = null) : Exception("قاعدهٔ پست رعایت نشده است.")
{
    public PostRule Rule { get; } = rule;
    public string? Field { get; } = field;
}
