using System.Text.Json;
using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Infra.AI.ContentReview;
using Xunit;
namespace BlogSpecLab.Tests.AI;
public class ContentReviewProtocolTests
{
    internal const string Clean = "{\"status\":\"completed\",\"summary\":\"خلاصه\",\"issues\":[],\"missingInformation\":[]}";
    internal static string Wire(string report = Clean, string status = "completed", string kind = "output_text") => JsonSerializer.Serialize(new {
        id = "resp_test", @object = "response", created_at = 0, model = "gpt-6-luna", status, error = (object?)null, incomplete_details = (object?)null,
        output = new[] { new { id = "msg_test", type = "message", role = "assistant", status = "completed",
            content = new[] { new { type = kind, text = report, refusal = "private refusal", annotations = Array.Empty<object>() } } } } });
    [Theory] [InlineData("{broken")] [InlineData("```json\n{}\n```")] [InlineData("{}")] [InlineData("[]")]
    [InlineData("{\"status\":\"completed\",\"status\":\"completed\",\"summary\":\"a\",\"issues\":[],\"missingInformation\":[]}")]
    public void Malformed_missing_duplicate_and_fenced_json_rejected(string text) => Assert.Null(ContentReviewOutputParser.Parse(text));
    [Fact] public void Unknown_type_enum_required_nullable_and_size_checked()
    {
        Assert.NotNull(ContentReviewOutputParser.Parse(Clean));
        Assert.Null(ContentReviewOutputParser.Parse(Clean.Replace("completed", "Completed")));
        Assert.Null(ContentReviewOutputParser.Parse(Clean.Replace("\"issues\":[]", "\"issues\":null")));
        Assert.Null(ContentReviewOutputParser.Parse(Clean.Replace("\"summary\":", "\"extra\":1,\"summary\":")));
        Assert.Null(ContentReviewOutputParser.Parse(Clean.Replace("خلاصه", new string('x', 256 * 1024))));
        Assert.Null(ContentReviewOutputParser.Parse(new string('[', 17) + "0" + new string(']', 17)));
        var issue = "{\"category\":\"writing\",\"field\":\"body\",\"evidence\":\"متن\",\"explanation\":\"شرح\",\"suggestion\":null}";
        Assert.NotNull(ContentReviewOutputParser.Parse(Clean.Replace("\"issues\":[]", "\"issues\":[" + issue + "]")));
        Assert.Null(ContentReviewOutputParser.Parse(Clean.Replace("\"issues\":[]", "\"issues\":[" + issue.Replace(",\"suggestion\":null", "") + "]")));
    }
    [Fact] public void Protocol_completed_independent_of_report_and_refusal_precedes_fragment()
    {
        Assert.Equal(ContentReviewOutcome.Candidate, ContentReviewOutputParser.ParseProtocol(Wire(Clean.Replace("completed", "needs_input"))).Outcome);
        Assert.Equal(ContentReviewOutcome.Refused, ContentReviewOutputParser.ParseProtocol(Wire("broken", "incomplete", "refusal")).Outcome);
        Assert.Equal(ContentReviewOutcome.InvalidOutput, ContentReviewOutputParser.ParseProtocol(Wire(status: "incomplete")).Outcome);
        Assert.Equal(ContentReviewOutcome.Unavailable, ContentReviewOutputParser.ParseProtocol(Wire(status: "failed")).Outcome);
        Assert.Equal(ContentReviewOutcome.InvalidOutput, ContentReviewOutputParser.ParseProtocol(Wire().Replace("\"type\":\"message\"", "\"type\":\"function_call\"")).Outcome);
        Assert.Equal(ContentReviewOutcome.InvalidOutput, ContentReviewOutputParser.ParseProtocol(Wire().Replace("\"role\":\"assistant\"", "\"role\":\"user\"")).Outcome);
    }
    [Fact] public void Reasoning_ignored_but_multiple_messages_missing_text_and_incomplete_message_rejected()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(Wire())!;
        var output = root["output"]!.AsArray();
        output.Insert(0, System.Text.Json.Nodes.JsonNode.Parse("{\"type\":\"reasoning\",\"summary\":[{\"text\":\"private reasoning\"}]}"));
        var valid = ContentReviewOutputParser.ParseProtocol(root.ToJsonString());
        Assert.Equal(ContentReviewOutcome.Candidate, valid.Outcome); Assert.DoesNotContain("private reasoning", JsonSerializer.Serialize(valid));
        output.Add(output[1]!.DeepClone()); Assert.Equal(ContentReviewOutcome.InvalidOutput, ContentReviewOutputParser.ParseProtocol(root.ToJsonString()).Outcome);
        output.RemoveAt(2); output[1]!["status"] = "incomplete";
        Assert.Equal(ContentReviewOutcome.InvalidOutput, ContentReviewOutputParser.ParseProtocol(root.ToJsonString()).Outcome);
        output[1]!["status"] = "completed"; output[1]!["content"]!.AsArray().Clear();
        Assert.Equal(ContentReviewOutcome.InvalidOutput, ContentReviewOutputParser.ParseProtocol(root.ToJsonString()).Outcome);
    }
}
