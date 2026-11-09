using System.Net;
using System.Text;
using System.Text.Json;
using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Infra.AI.ContentReview;
using Xunit;

namespace BlogSpecLab.Tests.AI;

public class ToolContentReviewTransportTests
{
    private sealed class TwoResponses : HttpMessageHandler
    {
        public readonly List<string> Requests = [];
        public string First = FunctionCall();
        public string Second = ContentReviewProtocolTests.Wire();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(ct));
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(Requests.Count == 1 ? First : Second, Encoding.UTF8, "application/json")
            };
        }

        public static string FunctionCall(string name = ReadCurrentPostTool.Name,
            string arguments = """{"scope":"current_post"}""") =>
            JsonSerializer.Serialize(new
            {
                id = "resp_tool", @object = "response", created_at = 0, model = "gpt-6-luna",
                status = "completed", error = (object?)null, incomplete_details = (object?)null,
                output = new[] { new
                {
                    id = "fc_tool", type = "function_call", status = "completed", name,
                    call_id = "call_current_post", arguments
                } }
            });
    }

    [Fact]
    public async Task Sdk_executes_exactly_one_tool_then_requests_existing_structured_report()
    {
        using var handler = new TwoResponses(); using var client = new HttpClient(handler);
        var gateway = new OpenAiToolContentReviewGateway("synthetic-key", new(), client);
        var result = await gateway.ReviewAsync(new("عنوان محرمانه", "متن محرمانه"), TestContext.Current.CancellationToken);
        Assert.Equal(ContentReviewOutcome.Candidate, result.Outcome);
        Assert.Equal(2, handler.Requests.Count);
        using var first = JsonDocument.Parse(handler.Requests[0]);
        using var second = JsonDocument.Parse(handler.Requests[1]);
        Assert.False(first.RootElement.GetProperty("store").GetBoolean());
        Assert.False(first.RootElement.GetProperty("stream").GetBoolean());
        Assert.DoesNotContain("عنوان محرمانه", handler.Requests[0]);
        Assert.DoesNotContain("متن محرمانه", handler.Requests[0]);
        Assert.Equal("function", first.RootElement.GetProperty("tools")[0].GetProperty("type").GetString());
        Assert.Equal(ReadCurrentPostTool.Name, first.RootElement.GetProperty("tools")[0].GetProperty("name").GetString());
        Assert.True(first.RootElement.GetProperty("tools")[0].GetProperty("strict").GetBoolean());
        Assert.Equal("json_schema", second.RootElement.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.False(second.RootElement.TryGetProperty("tools", out _));
        Assert.False(second.RootElement.GetProperty("store").GetBoolean());
        Assert.Contains("عنوان محرمانه", handler.Requests[1]);
        Assert.Contains("متن محرمانه", handler.Requests[1]);
        Assert.Contains("call_current_post", handler.Requests[1]);
        Assert.DoesNotContain("synthetic-key", handler.Requests[1]);
    }

    [Theory]
    [InlineData("other_tool", """{"scope":"current_post"}""")]
    [InlineData("read_current_post", """{"scope":"other_post"}""")]
    [InlineData("read_current_post", """{"scope":"current_post","postId":"other"}""")]
    public async Task Invalid_tool_requests_never_return_private_content(string name, string arguments)
    {
        using var handler = new TwoResponses { First = TwoResponses.FunctionCall(name, arguments) };
        using var client = new HttpClient(handler);
        var result = await new OpenAiToolContentReviewGateway("synthetic-key", new(), client)
            .ReviewAsync(new("عنوان خصوصی", "متن خصوصی"), TestContext.Current.CancellationToken);
        Assert.Equal(ContentReviewOutcome.InvalidOutput, result.Outcome);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain("متن خصوصی", handler.Requests[0]);
    }

    [Fact]
    public async Task Model_without_tool_call_does_not_return_success()
    {
        using var handler = new TwoResponses { First = ContentReviewProtocolTests.Wire() };
        using var client = new HttpClient(handler);
        var result = await new OpenAiToolContentReviewGateway("synthetic-key", new(), client)
            .ReviewAsync(new("عنوان", "متن"), TestContext.Current.CancellationToken);
        Assert.Equal(ContentReviewOutcome.InvalidOutput, result.Outcome);
        Assert.Single(handler.Requests);
    }
}
