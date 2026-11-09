using System.Net;
using BlogSpecLab.Core.Application.ContentReview;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BlogSpecLab.Tests.Integration;

public class ToolContentReviewApiTests
{
    private sealed class FakeToolGateway : IToolContentReviewGateway
    {
        public int Calls;
        public Task<ContentReviewGatewayResult> ReviewAsync(ContentReviewInput input, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new ContentReviewGatewayResult(ContentReviewOutcome.Candidate,
                new ContentReviewReport("completed", "گزارش کمکی، نه تأیید انتشار", [], [])));
        }
    }

    [Fact]
    public async Task Authorized_draft_can_use_tool_path_without_mutating_post()
    {
        var fake = new FakeToolGateway();
        await using var factory = new ContentReviewApiFactory
        {
            OverrideServices = services =>
            {
                services.RemoveAll<IToolContentReviewGateway>();
                services.AddSingleton<IToolContentReviewGateway>(fake);
            }
        };
        var post = await factory.Prepare();
        using var client = factory.CreateAuthenticatedClient(factory.Users.Authors[0]);
        var uri = $"/api/author/posts/{post.Id}/content-review-tools";
        var before = await (await client.GetAsync($"/api/author/posts/{post.Id}", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var response = await client.PostAsync(uri, null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, fake.Calls);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var after = await (await client.GetAsync($"/api/author/posts/{post.Id}", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Unauthorized_and_nonempty_body_never_call_tool_gateway()
    {
        var fake = new FakeToolGateway();
        await using var factory = new ContentReviewApiFactory
        {
            OverrideServices = services =>
            {
                services.RemoveAll<IToolContentReviewGateway>();
                services.AddSingleton<IToolContentReviewGateway>(fake);
            }
        };
        var post = await factory.Prepare();
        using var editor = factory.CreateAuthenticatedClient(factory.Users.Editors[0]);
        using var author = factory.CreateAuthenticatedClient(factory.Users.Authors[0]);
        var uri = $"/api/author/posts/{post.Id}/content-review-tools";
        Assert.Equal(HttpStatusCode.Forbidden,
            (await editor.PostAsync(uri, null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await author.PostAsync(uri, new StringContent("""{"body":"forbidden"}"""),
                TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Tool_route_is_documented_without_write_headers()
    {
        await using var factory = new ContentReviewApiFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://localhost") });
        using var doc = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync(
            "/openapi/v1.json", TestContext.Current.CancellationToken));
        var route = doc.RootElement.GetProperty("paths")
            .GetProperty("/api/author/posts/{id}/content-review-tools").GetProperty("post");
        Assert.DoesNotContain("If-Match", route.GetRawText());
        Assert.DoesNotContain("ETag", route.GetProperty("responses").GetRawText());
        foreach (var status in new[] { "200", "400", "401", "403", "404", "409", "422", "502", "503", "504", "500" })
            Assert.True(route.GetProperty("responses").TryGetProperty(status, out _));
    }
}
