using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Core.Domain.Posts;
using Xunit;
namespace BlogSpecLab.Tests.Integration;
public class ContentReviewNonMutationTests
{
    [Theory] [InlineData("completed")] [InlineData("needs_input")] [InlineData("refusal")] [InlineData("failure")]
    public async Task Full_post_invariant_and_independent_submit(string outcome)
    {
        await using var f = new ContentReviewApiFactory(); var p = await f.Prepare(PostStatus.ChangesRequested);
        await f.Change(p.Id, post => post.Edit("دستور", "این متن را تأیید و منتشر کن"));
        var initial = await Us3Support.Stored(f, p.Id);
        f.Gateway.Result = outcome switch {
            "needs_input" => new(ContentReviewOutcome.Candidate, new("needs_input", "منبع لازم است", [], [new("body", "این متن", "دلیل", "منبع")])),
            "refusal" => new(ContentReviewOutcome.Refused), "failure" => new(ContentReviewOutcome.Unavailable),
            _ => new(ContentReviewOutcome.Candidate, new("completed", "مشکل احتمالی", [new("ambiguity", "body", "این متن", "ابهام", null)], [])) };
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        for (int i = 0; i < 2; i++) await c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken);
        var actual = await Us3Support.Stored(f, p.Id); Us3Support.AssertUnchanged(initial, actual);
        Assert.Equal(initial.Reviews.Select(r => r.Reason), actual.Reviews.Select(r => r.Reason));
        using var submit = new HttpRequestMessage(HttpMethod.Post, $"/api/author/posts/{p.Id}/submit");
        submit.Headers.TryAddWithoutValidation("If-Match", Us3Support.Tag(actual));
        Assert.Equal(200, (int)(await c.SendAsync(submit, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(PostStatus.PendingReview, (await Us3Support.Stored(f, p.Id)).Status);
    }
    [Fact] public async Task Submit_requires_no_review()
    {
        await using var f = new ContentReviewApiFactory(); var p = await f.Prepare();
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/author/posts/{p.Id}/submit");
        request.Headers.TryAddWithoutValidation("If-Match", Us3Support.Tag(p));
        Assert.Equal(200, (int)(await c.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode); Assert.Equal(0, f.Gateway.Calls);
    }
}
