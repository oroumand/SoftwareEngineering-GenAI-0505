using BlogSpecLab.Domain.Posts.Entities;
using BlogSpecLab.Domain.Posts.Errors;
using BlogSpecLab.Domain.Posts.ValueObjects;

namespace BlogSpecLab.Domain.Tests.Posts;

public sealed class PostTests
{
    [Fact]
    public void CreateDraft_WithValidValues_CreatesOwnedDraftWithoutPublicationTime()
    {
        var author = new AuthorId("author-1");

        var post = Post.CreateDraft(PostId.New(), author, new PostTitle("عنوان"), new PostContent("متن"));

        Assert.Equal(author, post.AuthorId);
        Assert.Equal(PostStatus.Draft, post.Status);
        Assert.Null(post.PublishedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValueObjects_WithBlankValues_RejectInvalidState(string value)
    {
        Assert.Throws<PostValidationException>(() => new PostTitle(value));
        Assert.Throws<PostValidationException>(() => new PostContent(value));
        Assert.Throws<PostValidationException>(() => new AuthorId(value));
    }

    [Fact]
    public void EditDraft_ByNonOwner_RejectsAndPreservesState()
    {
        var post = CreateDraft();

        Assert.Throws<PostOwnershipException>(() =>
            post.EditDraft(new AuthorId("author-2"), new PostTitle("عنوان جدید"), new PostContent("متن جدید")));

        Assert.Equal("عنوان", post.Title.Value);
        Assert.Equal("متن", post.Content.Value);
    }

    [Fact]
    public void EditDraft_WithAnInvalidNewValue_RejectsAndPreservesBothValues()
    {
        var post = CreateDraft();

        Assert.Throws<PostValidationException>(() =>
            post.EditDraft(new AuthorId("author-1"), new PostTitle("عنوان جدید"), new PostContent("  ")));

        Assert.Equal("عنوان", post.Title.Value);
        Assert.Equal("متن", post.Content.Value);
    }

    [Fact]
    public void Publish_ByOwner_ChangesStatusAndRecordsTheGivenTime()
    {
        var post = CreateDraft();
        var publicationTime = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

        post.Publish(new AuthorId("author-1"), publicationTime);

        Assert.Equal(PostStatus.Published, post.Status);
        Assert.Equal(publicationTime, post.PublishedAt);
    }

    [Fact]
    public void Publish_ByNonOwner_RejectsAndPreservesDraft()
    {
        var post = CreateDraft();

        Assert.Throws<PostOwnershipException>(() =>
            post.Publish(new AuthorId("author-2"), new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero)));

        Assert.Equal(PostStatus.Draft, post.Status);
        Assert.Null(post.PublishedAt);
    }

    [Fact]
    public void PublishedPost_CannotBeEditedOrPublishedAgain_AndKeepsItsState()
    {
        var post = CreateDraft();
        var publicationTime = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        post.Publish(new AuthorId("author-1"), publicationTime);

        Assert.Throws<PostStateException>(() =>
            post.EditDraft(new AuthorId("author-1"), new PostTitle("عنوان جدید"), new PostContent("متن جدید")));
        Assert.Throws<PostStateException>(() => post.Publish(new AuthorId("author-1"), publicationTime.AddMinutes(1)));

        Assert.Equal("عنوان", post.Title.Value);
        Assert.Equal("متن", post.Content.Value);
        Assert.Equal(PostStatus.Published, post.Status);
        Assert.Equal(publicationTime, post.PublishedAt);
    }

    private static Post CreateDraft() =>
        Post.CreateDraft(PostId.New(), new AuthorId("author-1"), new PostTitle("عنوان"), new PostContent("متن"));
}
