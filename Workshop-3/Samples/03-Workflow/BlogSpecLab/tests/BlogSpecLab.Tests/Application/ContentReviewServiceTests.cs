using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Tests.Fixtures;
using Xunit;
namespace BlogSpecLab.Tests.Application;
public class ContentReviewServiceTests
{
    [Theory] [InlineData(ContentReviewOutcome.Candidate)] [InlineData(ContentReviewOutcome.Refused)] [InlineData(ContentReviewOutcome.Unavailable)]
    public async Task Fresh_permission_and_ownership_precede_all_provider_outcomes(ContentReviewOutcome outcome)
    {
        var fake = new ContentReviewFake(); fake.Result = fake.Result with { Outcome = outcome };
        fake.BeforeReturn = () => fake.Access = new("author", false, false, false, true);
        using var deadline = new ContentReviewDeadline(TimeProvider.System, TestContext.Current.CancellationToken);
        Assert.Equal(OperationError.Forbidden, (await Service(fake).ReviewAsync("author", fake.Post!.PostId, deadline)).Failure!.Error);
        fake.Access = new("author", true, false, false, true);
        fake.BeforeReturn = () => fake.Post = fake.Post! with { OwnerId = "other" };
        Assert.Equal(OperationError.PostNotFound, (await Service(fake).ReviewAsync("author", fake.Post!.PostId, deadline)).Failure!.Error);
    }
    [Fact] public async Task Fake_candidate_must_pass_independent_validation()
    {
        var fake = new ContentReviewFake { Result = new(ContentReviewOutcome.Candidate, new("completed", "خلاصه", [new("writing", "body", "شاهد ساخته‌شده", "شرح", null)], [])) };
        using var deadline = new ContentReviewDeadline(TimeProvider.System, TestContext.Current.CancellationToken);
        Assert.Equal(OperationError.AiReviewInvalidOutput, (await Service(fake).ReviewAsync("author", fake.Post!.PostId, deadline)).Failure!.Error);
    }
    internal static PostContentReviewService Service(ContentReviewFake fake, TimeProvider? clock = null) => new(fake, fake, fake, new(), clock ?? TimeProvider.System);
    [Theory] [InlineData(PostStatus.Draft)] [InlineData(PostStatus.ChangesRequested)]
    public async Task Valid_review_sends_only_exact_text(PostStatus status)
    {
        var fake = new ContentReviewFake(); fake.Post = fake.Post! with { Status = status };
        using var deadline = new ContentReviewDeadline(TimeProvider.System, default);
        var result = await Service(fake).ReviewAsync("author", fake.Post.PostId, deadline);
        Assert.True(result.IsSuccess); Assert.Equal(new(fake.Post.Title, fake.Post.Body), fake.Input);
        Assert.Equal(1, result.Value!.Source.ContentRevision); Assert.True(result.Value.Freshness.IsCurrentContent);
    }
    [Theory] [InlineData("permission", OperationError.Forbidden)] [InlineData("inactive", OperationError.Forbidden)]
    [InlineData("owner", OperationError.PostNotFound)] [InlineData("deleted", OperationError.PostNotFound)]
    [InlineData("state", OperationError.InvalidState)] [InlineData("title", OperationError.InvalidInput)] [InlineData("body", OperationError.InvalidInput)]
    public async Task Failed_prechecks_do_not_call_provider(string change, OperationError error)
    {
        var fake = new ContentReviewFake(); fake.Post = change switch {
            "owner" => fake.Post! with { OwnerId = "other" }, "deleted" => fake.Post! with { IsDeleted = true },
            "state" => fake.Post! with { Status = PostStatus.PendingReview }, "title" => fake.Post! with { Title = " " },
            "body" => fake.Post! with { Body = "\t" }, _ => fake.Post };
        if (change is "permission" or "inactive") fake.Access = new("author", change != "permission", false, false, change != "inactive");
        using var deadline = new ContentReviewDeadline(TimeProvider.System, default);
        Assert.Equal(error, (await Service(fake).ReviewAsync("author", fake.Post!.PostId, deadline)).Failure!.Error);
        Assert.Equal(0, fake.Calls);
        if (change is "permission" or "inactive") Assert.Equal(0, fake.Reads);
    }
    [Fact] public async Task Completed_multiple_issues_remain_machine_readable()
    {
        var fake = new ContentReviewFake { Result = new(ContentReviewOutcome.Candidate, new("completed", "خلاصه",
            [new("writing", "body", "متن", "شرح نگارش", null), new("ambiguity", "title", "عنوان", "شرح ابهام", "پیشنهاد")], [])) };
        using var deadline = new ContentReviewDeadline(TimeProvider.System, TestContext.Current.CancellationToken);
        var result = await Service(fake).ReviewAsync("author", fake.Post!.PostId, deadline);
        Assert.True(result.IsSuccess); Assert.Equal(2, result.Value!.Report.Issues.Length);
    }
}
