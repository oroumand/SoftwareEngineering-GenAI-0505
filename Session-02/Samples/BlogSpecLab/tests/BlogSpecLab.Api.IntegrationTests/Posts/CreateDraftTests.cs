using System.Net;
using System.Net.Http.Json;
using BlogSpecLab.Api.Posts.Models;

namespace BlogSpecLab.Api.IntegrationTests.Posts;

public sealed class CreateDraftTests(PostApiFixture fixture) : IClassFixture<PostApiFixture>
{
    [Fact]
    public async Task CreateDraft_WithAuthenticatedAuthor_ReturnsDraftAndOpaqueETag_AndReaderCannotReadIt()
    {
        fixture.CurrentAuthor.AuthorId = "author-1";
        var client = fixture.Factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/posts", new WritePostRequest { Title = "عنوان", Content = "متن" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.True(createResponse.Headers.TryGetValues("ETag", out var eTags));
        var eTag = Assert.Single(eTags);
        Assert.StartsWith("\"", eTag, StringComparison.Ordinal);
        Assert.DoesNotContain("xmin", eTag, StringComparison.OrdinalIgnoreCase);
        var created = await createResponse.Content.ReadFromJsonAsync<DraftCreatedResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal("Draft", created!.Status);

        var readResponse = await client.GetAsync($"/api/posts/{created.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);
        Assert.DoesNotContain("عنوان", await readResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.DoesNotContain("متن", await readResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "متن")]
    [InlineData("عنوان", "   ")]
    public async Task CreateDraft_WithBlankTitleOrContent_Returns400AndCreatesNothing(string title, string content)
    {
        fixture.CurrentAuthor.AuthorId = "author-1";
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/posts", new WritePostRequest { Title = title, Content = content }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
