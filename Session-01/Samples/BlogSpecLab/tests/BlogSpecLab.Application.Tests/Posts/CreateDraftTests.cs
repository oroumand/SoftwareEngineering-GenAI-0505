using BlogSpecLab.Application.Posts.CreateDraft;
using BlogSpecLab.Application.Posts.Ports;
using BlogSpecLab.Domain.Posts.Entities;
using BlogSpecLab.Domain.Posts.ValueObjects;

namespace BlogSpecLab.Application.Tests.Posts;

public sealed class CreateDraftTests
{
    [Fact]
    public async Task HandleAsync_WithValidInput_CreatesAndCommitsAnOwnedDraft()
    {
        var repository = new RecordingPostRepository();
        var handler = new CreateDraftHandler(repository);

        var result = await handler.HandleAsync(
            new CreateDraftCommand("author-1", "عنوان", "متن"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(repository.AddedPost);
        Assert.Equal("author-1", repository.AddedPost!.AuthorId.Value);
        Assert.Equal(PostStatus.Draft, repository.AddedPost.Status);
        Assert.Null(repository.AddedPost.PublishedAt);
        Assert.Equal("version-1", result.Version.Value);
    }

    [Theory]
    [InlineData("", "متن")]
    [InlineData("عنوان", "   ")]
    public async Task HandleAsync_WithInvalidContent_DoesNotCommit(string title, string content)
    {
        var repository = new RecordingPostRepository();
        var handler = new CreateDraftHandler(repository);

        await Assert.ThrowsAsync<ValidationUseCaseException>(() => handler.HandleAsync(
            new CreateDraftCommand("author-1", title, content),
            TestContext.Current.CancellationToken));

        Assert.Null(repository.AddedPost);
    }

    private sealed class RecordingPostRepository : IPostRepository
    {
        public Post? AddedPost { get; private set; }

        public Task<PostVersion> AddAsync(Post post, CancellationToken cancellationToken)
        {
            AddedPost = post;
            return Task.FromResult(new PostVersion("version-1"));
        }

        public Task<LoadedPost?> FindByIdAsync(PostId postId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SavePostResult> SaveAsync(Post post, PostVersion expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<PublishedPostSummary>> ListPublishedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PublishedPostDetail?> FindPublishedByIdAsync(PostId postId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
