using System.Text.Json;
using Xunit;

namespace BlogSpecLab.Tests.Integration;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task GeneratedDocumentDescribesAllContractRoutesSecurityBodiesResponsesAndHeaders()
    {
        await using var factory = new BlogApiFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new("https://localhost") });
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = json.RootElement;
        var paths = root.GetProperty("paths");
        var expected = new Dictionary<string, string[]>
        {
            ["/api/author/posts"] = ["get", "post"],
            ["/api/author/posts/{id}"] = ["get", "put", "delete"],
            ["/api/author/posts/{id}/submit"] = ["post"],
            ["/api/editor/posts"] = ["get"],
            ["/api/editor/posts/{id}"] = ["get"],
            ["/api/editor/posts/{id}/request-changes"] = ["post"],
            ["/api/editor/posts/{id}/approve"] = ["post"],
            ["/api/editor/posts/{id}/publish"] = ["post"],
            ["/api/posts"] = ["get"],
            ["/api/posts/{id}"] = ["get"]
        };
        Assert.Equal(expected.Keys.Order(), paths.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        foreach (var (path, methods) in expected)
        {
            Assert.Equal(methods.Order(), paths.GetProperty(path).EnumerateObject().Select(p => p.Name).Order());
            foreach (var method in methods)
            {
                var operation = paths.GetProperty(path).GetProperty(method);
                var management = path.StartsWith("/api/author") || path.StartsWith("/api/editor");
                Assert.Equal(management, operation.TryGetProperty("security", out _));
                var responses = operation.GetProperty("responses");
                var success = method == "delete" ? "204" : path == "/api/author/posts" && method == "post" ? "201" : "200";
                Assert.True(responses.TryGetProperty(success, out var ok));
                if (management)
                {
                    Assert.True(responses.TryGetProperty("401", out var unauthorized));
                    Assert.True(unauthorized.GetProperty("headers").TryGetProperty("WWW-Authenticate", out _));
                    Assert.True(ok.GetProperty("headers").TryGetProperty("Cache-Control", out _));
                    if (success != "204" && (path.Contains("{id}") || success == "201"))
                        Assert.True(ok.GetProperty("headers").TryGetProperty("ETag", out _));
                }
                if (management && method != "get" && success != "201")
                {
                    Assert.Contains(operation.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "If-Match" && p.GetProperty("required").GetBoolean());
                    foreach (var status in new[] { "400", "409", "412", "428" })
                        Assert.True(responses.GetProperty(status).GetProperty("content").TryGetProperty("application/problem+json", out _));
                }
                var hasBody = method == "put" || success == "201" || path.EndsWith("request-changes");
                Assert.Equal(hasBody, operation.TryGetProperty("requestBody", out _));
            }
        }
        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.Equal(new[] { "body", "id", "publishedAt", "title" }, schemas.GetProperty("PublicPostView").GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
        Assert.True(schemas.GetProperty("PostView").GetProperty("properties").TryGetProperty("allowedActions", out _));
        Assert.True(schemas.GetProperty("ProblemDetails").GetProperty("properties").TryGetProperty("code", out _));
        Assert.False(schemas.GetProperty("PostContentRequest").GetProperty("additionalProperties").GetBoolean());
    }
}
