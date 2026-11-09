using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Tests.Fixtures;
using Xunit;
namespace BlogSpecLab.Tests.Application;
public class ContentReviewValidatorTests
{
    private readonly ContentReviewSnapshot snapshot = new(Guid.NewGuid(), "author", "عنوان", "متن می‌رود", 1, PostStatus.Draft, DateTimeOffset.UtcNow);
    [Theory] [InlineData("bad", "body", "متن", "شرح", null)] [InlineData("writing", "unknown", "متن", "شرح", null)]
    [InlineData("writing", "body", "می رود", "شرح", null)] [InlineData("writing", "body", "ي", "شرح", null)]
    [InlineData("writing", "body", "متن", " ", null)] [InlineData("writing", "body", "متن", "شرح", " ")]
    public void Invalid_candidates_rejected(string category, string field, string evidence, string explanation, string? suggestion) =>
        Assert.False(new ContentReviewReportValidator().IsValid(new("completed", "خلاصه", [new(category, field, evidence, explanation, suggestion)], []), snapshot));
    [Fact] public void Shared_exact_evidence_allowed() => Assert.True(new ContentReviewReportValidator().IsValid(
        new("completed", "خلاصه", [new("writing", "body", "متن", "شرح", null), new("ambiguity", "body", "متن", "شرح", "پیشنهاد")], []), snapshot));
    [Fact] public void Null_and_status_array_inconsistencies_rejected()
    {
        var validator = new ContentReviewReportValidator(); Assert.False(validator.IsValid(null, snapshot));
        Assert.False(validator.IsValid(ContentReviewFake.Clean with { Status = "needs_input" }, snapshot));
        Assert.False(validator.IsValid(ContentReviewFake.Clean with { Summary = " " }, snapshot));
        Assert.False(validator.IsValid(ContentReviewFake.Clean with { MissingInformation = [new("body", "متن", "دلیل", "منبع")] }, snapshot));
    }
}
