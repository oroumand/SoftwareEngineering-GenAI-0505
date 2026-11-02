using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Infra.AI.ContentReview;
using Xunit;
namespace BlogSpecLab.Tests.AI;
public class ContentReviewProviderTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("BLOG_AI_PROVIDER_OPT_IN") == "true";
    internal static OpenAiContentReviewGateway Gateway()
    {
        var key = Environment.GetEnvironmentVariable("AI__ContentReview__ApiKey");
        Assert.False(string.IsNullOrWhiteSpace(key), "اجرای صریح Provider نیازمند secret محیطی است؛ نبود key پذیرش نیست.");
        return new(key!, Options());
    }
    internal static ContentReviewOptions Options() => new() { Enabled = true,
        Model = Environment.GetEnvironmentVariable("AI__ContentReview__Model") ?? "gpt-6-luna",
        ReasoningEffort = Environment.GetEnvironmentVariable("AI__ContentReview__ReasoningEffort") };
    [Fact(SkipUnless = nameof(Enabled))] [Trait("Category", "AIProvider")]
    public async Task Real_schema_access_parsing_and_cancellation()
    {
        var gateway = Gateway(); using var budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(50));
        var input = new ContentReviewInput("یادداشت آموزشی", "این یادداشت دربارهٔ نوشتن عنوان روشن است.");
        var result = await gateway.ReviewAsync(input, budget.Token);
        Assert.Equal(ContentReviewOutcome.Candidate, result.Outcome); Assert.NotNull(result.ProviderModel);
        var snapshot = new ContentReviewSnapshot(Guid.NewGuid(), "synthetic", input.Title, input.Body, 1,
            Core.Domain.Posts.PostStatus.Draft, DateTimeOffset.UtcNow);
        Assert.True(new ContentReviewReportValidator().IsValid(result.Report, snapshot));
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = gateway.ReviewAsync(input, cancelled.Token); cancelled.CancelAfter(TimeSpan.FromMilliseconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}
