using System.Text.Json;
using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Infra.AI.ContentReview;
using Xunit;

namespace BlogSpecLab.Tests.AI;

public class ReadCurrentPostToolTests
{
    [Fact]
    public void Only_current_snapshot_can_be_read()
    {
        var input = new ContentReviewInput("عنوان آزمایشی", "متن خصوصی");
        Assert.True(ReadCurrentPostTool.TryExecute("""{"scope":"current_post"}""", input, out var result));
        using var document = JsonDocument.Parse(result);
        Assert.Equal(input.Title, document.RootElement.GetProperty("title").GetString());
        Assert.Equal(input.Body, document.RootElement.GetProperty("body").GetString());
        Assert.False(document.RootElement.TryGetProperty("postId", out _));
        Assert.False(document.RootElement.TryGetProperty("ownerId", out _));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"scope":"other_post"}""")]
    [InlineData("""{"scope":"current_post","postId":"not-allowed"}""")]
    [InlineData("""{"scope":"current_post","scope":"current_post"}""")]
    [InlineData("""{"scope":null}""")]
    [InlineData("[]")]
    [InlineData("{broken")]
    public void Invalid_or_extra_arguments_fail_closed(string arguments)
    {
        Assert.False(ReadCurrentPostTool.TryExecute(arguments, new("عنوان", "متن"), out var result));
        Assert.Equal(string.Empty, result);
    }
}
