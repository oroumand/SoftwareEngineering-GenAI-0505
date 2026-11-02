using System.Text.Json;
using Xunit;
namespace BlogSpecLab.Tests.Integration;
public class ContentReviewOpenApiTests
{
    [Fact] public async Task Review_metadata_matches_readonly_contract()
    {
        await using var f = new ContentReviewApiFactory(); using var c = f.CreateClient(new() { BaseAddress = new("https://localhost") });
        var document = JsonDocument.Parse(await c.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/author/posts/{id}/content-review").GetProperty("post");
        Assert.DoesNotContain("If-Match", operation.GetRawText()); Assert.DoesNotContain("ETag", operation.GetProperty("responses").GetRawText());
        foreach (var status in new[] { "200", "400", "401", "403", "404", "409", "422", "502", "503", "504", "500" })
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas").GetRawText();
        foreach (var token in new[] { "needs_input", "unsupported_claim", "suggestion", "missingInformation", "capturedAt", "requiresNewReview" }) Assert.Contains(token, schemas);
        var issue = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ContentReviewIssue");
        Assert.Contains("suggestion", issue.GetProperty("required").EnumerateArray().Select(p => p.GetString()));
        Assert.Contains("null", issue.GetProperty("properties").GetProperty("suggestion").GetRawText());
    }
}
