using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Tests.Fixtures;
using Xunit;

namespace BlogSpecLab.Tests.Domain;

public sealed class PostAuthorTests
{
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" \t\n")] [InlineData("\u2003")]
    public void InvalidContentIsRejectedWithoutMutation(string? text)
    {
        Assert.Equal(PostRule.InvalidInput, Assert.Throws<PostRuleException>(() => Post.Create("author", text, "body")).Rule);
        Assert.Throws<PostRuleException>(() => Post.Create("author", "title", text));
        var post = Post.Create("author", "title", "body");
        var version = post.Version;
        Assert.Throws<PostRuleException>(() => post.Edit(text, "body"));
        Assert.Throws<PostRuleException>(() => post.Edit("title", text));
        Assert.Equal("title", post.Title);
        Assert.Equal(version, post.Version);
        Assert.Equal(1, post.ContentRevision);
    }

    [Fact]
    public void CreatePreservesUnicodeAndServerIdentityAndStartsDraft()
    {
        const string text = "  فارسی e\u0301 😀\n";
        var post = Post.Create("author", text, text);
        Assert.NotEqual(Guid.Empty, post.Id);
        Assert.NotEqual(Guid.Empty, post.Version);
        Assert.Equal("author", post.OwnerId);
        Assert.Equal(PostStatus.Draft, post.Status);
        Assert.Equal(1, post.ContentRevision);
        Assert.Equal(text, post.Title);
        Assert.Equal(text, post.Body);
        Assert.False(post.IsDeleted);
        Assert.Null(post.Publication);
        Assert.Null(post.CurrentReviewId);
        Assert.Empty(post.Reviews);
        Assert.Throws<PostRuleException>(() => Post.Create(" ", "title", "body"));
    }

    [Theory]
    [InlineData(PostStatus.Draft)] [InlineData(PostStatus.PendingReview)]
    [InlineData(PostStatus.ChangesRequested)] [InlineData(PostStatus.Approved)]
    public void IdenticalEditDoesNotChangeRevisionVersionOrDecision(PostStatus status)
    {
        var post = PostStates.In(status);
        var version = post.Version;
        var reviewId = post.CurrentReviewId;
        post.Edit(post.Title, post.Body);
        Assert.Equal(status, post.Status);
        Assert.Equal(version, post.Version);
        Assert.Equal(1, post.ContentRevision);
        Assert.Equal(reviewId, post.CurrentReviewId);
    }

    [Theory]
    [InlineData(PostStatus.Draft, PostStatus.Draft)]
    [InlineData(PostStatus.PendingReview, PostStatus.Draft)]
    [InlineData(PostStatus.ChangesRequested, PostStatus.ChangesRequested)]
    [InlineData(PostStatus.Approved, PostStatus.Draft)]
    public void RealEditChangesRevisionAndVersionAndPreservesHistory(PostStatus initial, PostStatus expected)
    {
        var post = PostStates.In(initial);
        var version = post.Version;
        var history = post.Reviews.ToArray();
        var reviewId = post.CurrentReviewId;
        post.Edit(post.Title + "!", post.Body);
        Assert.Equal(expected, post.Status);
        Assert.Equal(2, post.ContentRevision);
        Assert.NotEqual(version, post.Version);
        Assert.Equal(history, post.Reviews);
        Assert.Equal(reviewId, post.CurrentReviewId);
        Assert.Equal("author", post.OwnerId);
        post.Edit(post.Title, post.Body + "!");
        Assert.Equal(3, post.ContentRevision);
    }

    [Theory]
    [InlineData(PostStatus.Draft)] [InlineData(PostStatus.ChangesRequested)]
    public void SubmitChangesStateAndVersionOnlyAndCannotRepeat(PostStatus initial)
    {
        var post = PostStates.In(initial);
        var version = post.Version;
        post.Submit();
        Assert.Equal(PostStatus.PendingReview, post.Status);
        Assert.Equal(1, post.ContentRevision);
        Assert.NotEqual(version, post.Version);
        var submitted = post.Version;
        Assert.Equal(PostRule.InvalidState, Assert.Throws<PostRuleException>(() => post.Submit()).Rule);
        Assert.Equal(submitted, post.Version);
    }

    [Theory]
    [InlineData(PostStatus.PendingReview)] [InlineData(PostStatus.Approved)]
    public void SubmitFromOtherUnpublishedStatesFails(PostStatus status) =>
        Assert.Equal(PostRule.InvalidState, Assert.Throws<PostRuleException>(() => PostStates.In(status).Submit()).Rule);

    [Theory]
    [InlineData(PostStatus.Draft)] [InlineData(PostStatus.PendingReview)]
    [InlineData(PostStatus.ChangesRequested)] [InlineData(PostStatus.Approved)]
    public void DeleteIsLogicalAndBlocksEveryAuthorMutation(PostStatus status)
    {
        var post = PostStates.In(status);
        var version = post.Version;
        post.Delete();
        Assert.True(post.IsDeleted);
        Assert.NotEqual(version, post.Version);
        Assert.Equal(1, post.ContentRevision);
        Assert.Throws<PostRuleException>(() => post.Edit("new", "body"));
        Assert.Throws<PostRuleException>(() => post.Submit());
        Assert.Throws<PostRuleException>(() => post.Delete());
    }

    [Fact]
    public void PublishedRejectsAllAuthorChangesIncludingIdenticalEdit()
    {
        var post = PostStates.In(PostStatus.Published);
        var version = post.Version;
        foreach (var action in new Action[] { () => post.Edit(post.Title, post.Body), () => post.Delete(), () => post.Submit() })
            Assert.Equal(PostRule.PublishedPostImmutable, Assert.Throws<PostRuleException>(action).Rule);
        Assert.Equal(version, post.Version);
        Assert.False(post.IsDeleted);
    }

    [Theory]
    [InlineData("Title")] [InlineData("Body")]
    public void SubmitRevalidatesContentWithoutChangingVersion(string field)
    {
        var post = Post.Create("author", "title", "body");
        typeof(Post).GetProperty(field)!.SetValue(post, " \t");
        var version = post.Version;
        Assert.Equal(PostRule.InvalidInput, Assert.Throws<PostRuleException>(() => post.Submit()).Rule);
        Assert.Equal(version, post.Version); Assert.Equal(PostStatus.Draft, post.Status);
    }

    [Fact]
    public void ApprovalValidityUsesCurrentDecisionRevisionAndExactSnapshot()
    {
        var post = PostStates.In(PostStatus.Approved);
        Assert.True(post.HasValidApproval());
        post.Edit(post.Title, post.Body); Assert.True(post.HasValidApproval());
        post.Edit(post.Title + "!", post.Body); Assert.False(post.HasValidApproval());
        Assert.Single(post.Reviews);
        Assert.Equal("عنوان", post.Reviews.Single().ReviewedTitle);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" \t")]
    public void ImmutableReviewStructureRejectsEmptyChangeReason(string? reason) =>
        Assert.Throws<PostRuleException>(() => new ReviewDecision(Guid.NewGuid(), Guid.NewGuid(), 1, "title", "body",
            ReviewDecisionKind.RequestChanges, "reviewer", DateTime.UtcNow, reason));
}
