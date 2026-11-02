using System.Text;
using BlogSpecLab.Core.Application.ContentReview;
using Xunit;
namespace BlogSpecLab.Tests.Integration;
public class ContentReviewNeedsInputApiTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task New_request_after_etag_edit_uses_new_snapshot(bool mixed)
    {
        await using var f = new ContentReviewApiFactory(); var p = await f.Prepare();
        f.Gateway.Result = new(ContentReviewOutcome.Candidate, new("needs_input", "اطلاعات لازم است",
            mixed ? [new("writing", "body", "متن", "شرح", null)] : [], [new("body", "متن", "دلیل", "منبع")]));
        using var client = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        var first = await ContentReviewApiTests.Json(await client.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken));
        Assert.Equal("needs_input", first.GetProperty("report").GetProperty("status").GetString());
        Assert.Single(first.GetProperty("report").GetProperty("missingInformation").EnumerateArray());
        using var edit = new HttpRequestMessage(HttpMethod.Put, $"/api/author/posts/{p.Id}") { Content = new StringContent("{\"title\":\"تازه\",\"body\":\"متن تازه با منبع\"}", Encoding.UTF8, "application/json") };
        edit.Headers.TryAddWithoutValidation("If-Match", Us3Support.Tag(p)); Assert.Equal(200, (int)(await client.SendAsync(edit, TestContext.Current.CancellationToken)).StatusCode);
        var second = await ContentReviewApiTests.Json(await client.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken));
        Assert.Equal(2, second.GetProperty("source").GetProperty("contentRevision").GetInt32());
        Assert.Equal("متن تازه با منبع", f.Gateway.Input!.Body);
    }
}
