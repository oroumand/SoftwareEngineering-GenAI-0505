using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Tests.Fixtures;
using Xunit;
namespace BlogSpecLab.Tests.Application;
public class ContentReviewNeedsInputTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Missing_information_and_known_issues_are_independently_extractable(bool mixed)
    {
        var fake = new ContentReviewFake();
        fake.Result = new(ContentReviewOutcome.Candidate, new("needs_input", "منبع لازم است",
            mixed ? [new("writing", "body", "متن", "شرح", null)] : [], [new("body", "متن", "دلیل", "منبع")]));
        using var deadline = new ContentReviewDeadline(TimeProvider.System, default);
        var result = await ContentReviewServiceTests.Service(fake).ReviewAsync("author", fake.Post!.PostId, deadline);
        Assert.True(result.IsSuccess); Assert.Single(result.Value!.Report.MissingInformation); Assert.Equal(mixed ? 1 : 0, result.Value.Report.Issues.Length);
        fake.Post = fake.Post with { Body = "متن جدید", ContentRevision = 2 }; fake.Result = new(ContentReviewOutcome.Candidate, ContentReviewFake.Clean);
        var second = await ContentReviewServiceTests.Service(fake).ReviewAsync("author", fake.Post.PostId, deadline);
        Assert.Equal(2, second.Value!.Source.ContentRevision); Assert.Equal("متن جدید", fake.Input!.Body); Assert.Equal(2, fake.Calls);
    }
}
