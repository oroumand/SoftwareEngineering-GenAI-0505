using System.Net;
using System.Text.Json;
using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Core.Domain.Posts;
using Xunit;
namespace BlogSpecLab.Tests.Integration;
public class ContentReviewApiTests
{
    [Theory] [InlineData(ContentReviewOutcome.Candidate, 200)] [InlineData(ContentReviewOutcome.Refused, 422)]
    [InlineData(ContentReviewOutcome.InvalidOutput, 502)] [InlineData(ContentReviewOutcome.Unavailable, 503)] [InlineData(ContentReviewOutcome.Timeout, 504)]
    public async Task Outcomes_have_distinct_http_and_no_store(ContentReviewOutcome outcome, int status)
    {
        await using var f = new ContentReviewApiFactory(); var p = await f.Prepare();
        f.Gateway.Result = f.Gateway.Result with { Outcome = outcome };
        using var client = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        var response = await client.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken);
        Assert.Equal(status, (int)response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Null(response.Headers.ETag);
        if (status == 200) Assert.Equal("completed", (await Json(response)).GetProperty("report").GetProperty("status").GetString());
        else Assert.StartsWith("ai_review_", (await Json(response)).GetProperty("code").GetString());
    }
    [Theory] [InlineData("anonymous", 401)] [InlineData("editor", 403)] [InlineData("other", 404)]
    [InlineData("absent", 404)] [InlineData("state", 409)] [InlineData("body", 400)] [InlineData("id", 400)]
    public async Task Rejected_requests_do_not_call_gateway(string scenario, int status)
    {
        await using var f = new ContentReviewApiFactory(); var p = await f.Prepare(scenario == "state" ? PostStatus.PendingReview : PostStatus.Draft);
        using var client = scenario == "anonymous" ? f.CreateClient(new() { BaseAddress = new("https://localhost") })
            : f.CreateAuthenticatedClient(scenario == "editor" ? f.Users.Editors[0] : scenario == "other" ? f.Users.Authors[1] : f.Users.Authors[0]);
        var route = scenario == "id" ? "/api/author/posts/not-a-guid/content-review" : ContentReviewApiFactory.Route(scenario == "absent" ? Guid.NewGuid() : p.Id);
        var response = await client.PostAsync(route, scenario == "body" ? new StringContent("{}") : null, TestContext.Current.CancellationToken);
        Assert.Equal(status, (int)response.StatusCode); Assert.Equal(0, f.Gateway.Calls); Assert.True(response.Headers.CacheControl!.NoStore);
    }
    internal static async Task<JsonElement> Json(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.Clone();
}
