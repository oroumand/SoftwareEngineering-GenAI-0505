using System.Net;
using System.Text;
using System.Text.Json;
using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Infra.AI.ContentReview;
using Xunit;
namespace BlogSpecLab.Tests.AI;
public class ContentReviewTransportTests
{
    internal sealed class Transport : HttpMessageHandler
    {
        public string? Body; public Uri? Uri; public int Calls; public HttpStatusCode Status = HttpStatusCode.OK;
        public string Response = ContentReviewProtocolTests.Wire(); public bool Pause; public bool FailNetwork; public CancellationToken ObservedToken;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Uri = request.RequestUri; Body = await request.Content!.ReadAsStringAsync(ct); ObservedToken = ct;
            if (FailNetwork) throw new HttpRequestException("synthetic private transport failure");
            if (Pause) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new(Status) { Content = new StringContent(Response, Encoding.UTF8, "application/json") };
        }
    }
    [Fact] public async Task Actual_sdk_serialization_enforces_exact_artifact_and_minimizes_input()
    {
        using var handler = new Transport(); using var client = new HttpClient(handler);
        var gateway = new OpenAiContentReviewGateway("synthetic-key", new(), client);
        var result = await gateway.ReviewAsync(new("عنوان", "متن"), TestContext.Current.CancellationToken);
        Assert.Equal(ContentReviewOutcome.Candidate, result.Outcome); Assert.Equal("/v1/responses", handler.Uri!.AbsolutePath);
        using var doc = JsonDocument.Parse(handler.Body!); var root = doc.RootElement;
        Assert.False(root.GetProperty("store").GetBoolean()); Assert.False(root.GetProperty("stream").GetBoolean());
        var format = root.GetProperty("text").GetProperty("format"); Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean()); Assert.Equal("content_review_v1", format.GetProperty("name").GetString());
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(ContentReviewOutputParser.SchemaJson).RootElement, format.GetProperty("schema")));
        foreach (var forbidden in new[] { "tools", "conversation", "previous_response_id", "metadata", "user" }) Assert.False(root.TryGetProperty(forbidden, out _));
        var user = root.GetProperty("input")[1]; Assert.Equal("user", user.GetProperty("role").GetString());
        var content = user.GetProperty("content"); var text = content.ValueKind == JsonValueKind.String ? content.GetString() : content[0].GetProperty("text").GetString();
        using var input = JsonDocument.Parse(text!); Assert.Equal(2, input.RootElement.EnumerateObject().Count());
        Assert.Equal("متن", input.RootElement.GetProperty("body").GetString()); Assert.DoesNotContain("synthetic-key", handler.Body!);
        var artifact = Path.Combine(FindRoot(), "specs/002-ai-content-review/contracts/model-output.schema.json");
        Assert.Equal(File.ReadAllText(artifact), ContentReviewOutputParser.SchemaJson);
    }
    [Theory] [InlineData(401)] [InlineData(429)] [InlineData(500)] [InlineData(503)]
    public async Task Provider_errors_safe_and_no_retry(int status)
    {
        using var handler = new Transport { Status = (HttpStatusCode)status, Response = "{\"error\":{\"message\":\"private\",\"type\":\"server_error\"}}" };
        using var client = new HttpClient(handler); var gateway = new OpenAiContentReviewGateway("synthetic-key", new(), client);
        Assert.Equal(ContentReviewOutcome.Unavailable, (await gateway.ReviewAsync(new("عنوان", "متن"), TestContext.Current.CancellationToken)).Outcome); Assert.Equal(1, handler.Calls);
    }
    [Fact] public async Task Cancellation_is_propagated()
    {
        using var handler = new Transport { Pause = true }; using var client = new HttpClient(handler);
        using var cancel = new CancellationTokenSource(); var gateway = new OpenAiContentReviewGateway("synthetic-key", new(), client);
        var task = gateway.ReviewAsync(new("عنوان", "متن"), cancel.Token); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task); Assert.True(handler.ObservedToken.IsCancellationRequested);
    }
    [Fact] public async Task Network_failure_has_safe_outcome()
    {
        using var handler = new Transport { FailNetwork = true }; using var client = new HttpClient(handler);
        var result = await new OpenAiContentReviewGateway("synthetic-key", new(), client).ReviewAsync(new("عنوان", "متن"), TestContext.Current.CancellationToken);
        Assert.Equal(ContentReviewOutcome.Unavailable, result.Outcome); Assert.Null(result.Report); Assert.Equal(1, handler.Calls);
    }
    internal static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "BlogSpecLab.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("ریشهٔ corpus پیدا نشد.");
    }
}
