using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Tests.Fixtures;
using Xunit;
namespace BlogSpecLab.Tests.Domain;
public sealed class PostReviewTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void ReturnResubmitAndApprovePreserveHistory()
    {
        var p = PostStates.In(PostStatus.PendingReview); var v = p.Version;
        p.RequestChanges(p.OwnerId, Now, "reason");
        Assert.Equal(PostStatus.ChangesRequested, p.Status); Assert.NotEqual(v, p.Version);
        var old = Assert.Single(p.Reviews); Assert.Equal(p.CurrentReviewId, old.Id);
        Assert.Equal(p.Title, old.ReviewedTitle); Assert.Equal(p.Body, old.ReviewedBody);
        Assert.Equal(1, old.ContentRevision); Assert.Equal(Now, old.DecidedAt);
        p.Edit("new", "new body"); p.Submit(); p.Approve("editor", Now.AddMinutes(1));
        Assert.Equal(PostStatus.Approved, p.Status); Assert.True(p.HasValidApproval());
        Assert.Equal(2, p.Reviews.Count); Assert.Equal("عنوان", old.ReviewedTitle); Assert.Equal("reason", old.Reason);
        Assert.All(typeof(ReviewDecision).GetProperties(), property => Assert.Null(property.SetMethod));
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" \t\u2003")]
    public void InvalidReasonDoesNotMutate(string? reason)
    {
        var p = PostStates.In(PostStatus.PendingReview); var v = p.Version;
        Assert.Equal(PostRule.InvalidInput, Assert.Throws<PostRuleException>(() => p.RequestChanges("editor", Now, reason)).Rule);
        Assert.Equal(v, p.Version); Assert.Empty(p.Reviews); Assert.Equal(PostStatus.PendingReview, p.Status);
    }
    [Fact]
    public void SelfApprovalFailsAndContentChangeInvalidatesEvenRestoredContent()
    {
        var p = PostStates.In(PostStatus.PendingReview); var v = p.Version;
        Assert.Throws<PostRuleException>(() => p.Approve(p.OwnerId, Now)); Assert.Equal(v, p.Version); Assert.Empty(p.Reviews);
        p.Approve("editor", Now); var review = Assert.Single(p.Reviews); v = p.Version;
        p.Edit(p.Title, p.Body); Assert.True(p.HasValidApproval()); Assert.Equal(v, p.Version);
        p.Edit("changed", p.Body); Assert.False(p.HasValidApproval()); Assert.Equal(PostStatus.Draft, p.Status);
        p.Edit(review.ReviewedTitle, review.ReviewedBody); Assert.False(p.HasValidApproval());
        Assert.Throws<PostRuleException>(() => p.Publish("publisher", Now)); Assert.Null(p.Publication); Assert.Equal(1, review.ContentRevision);
    }
    [Theory]
    [InlineData(PostStatus.Draft)] [InlineData(PostStatus.ChangesRequested)] [InlineData(PostStatus.Approved)] [InlineData(PostStatus.Published)]
    public void ReviewOnlyAcceptsPending(PostStatus status)
    {
        var p = PostStates.In(status); var v = p.Version; var count = p.Reviews.Count;
        Assert.Throws<PostRuleException>(() => p.Approve("editor", Now)); Assert.Throws<PostRuleException>(() => p.RequestChanges("editor", Now, "reason"));
        Assert.Equal(v, p.Version); Assert.Equal(count, p.Reviews.Count);
    }
    [Fact]
    public void PublishRequiresApprovalAndKeepsFirstPublication()
    {
        var p = PostStates.In(PostStatus.PendingReview); Assert.Throws<PostRuleException>(() => p.Publish("publisher", Now));
        p.Approve("editor", Now); p.Publish(p.OwnerId, Now.AddMinutes(1));
        Assert.Equal(PostStatus.Published, p.Status); var first = p.Publication; var v = p.Version;
        Assert.Equal(p.CurrentReviewId, first!.ApprovedReviewId);
        p.Publish("another", Now.AddDays(1)); Assert.Same(first, p.Publication); Assert.Equal(v, p.Version);
        Assert.Throws<PostRuleException>(() => p.Edit("new", "new")); Assert.Throws<PostRuleException>(p.Delete);
    }
    [Fact]
    public void MismatchedSnapshotRevisionOrCurrentDecisionCannotPublish()
    {
        foreach (var field in new[] { nameof(Post.Title), nameof(Post.Body), nameof(Post.ContentRevision), nameof(Post.CurrentReviewId) })
        {
            var p = PostStates.In(PostStatus.Approved);
            object value = field == nameof(Post.ContentRevision) ? 2 : field == nameof(Post.CurrentReviewId) ? Guid.NewGuid() : "tampered";
            typeof(Post).GetProperty(field)!.SetValue(p, value);
            Assert.False(p.HasValidApproval()); Assert.Throws<PostRuleException>(() => p.Publish("publisher", Now)); Assert.Null(p.Publication);
        }
        var deleted = PostStates.In(PostStatus.PendingReview); deleted.Delete();
        Assert.Throws<PostRuleException>(() => deleted.Approve("editor", Now));
        Assert.Throws<PostRuleException>(() => deleted.RequestChanges("editor", Now, "reason"));
        Assert.Throws<PostRuleException>(() => deleted.Publish("publisher", Now));
    }
}
