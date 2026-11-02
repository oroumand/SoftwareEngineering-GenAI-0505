using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Tests.Fixtures;
using Xunit;
namespace BlogSpecLab.Tests.Application;
public class ContentReviewDeadlineTests
{
    [Fact] public void Monotonic_budget_reserves_validation_and_caps_provider()
    {
        var clock = new ReviewClock(); using var deadline = new ContentReviewDeadline(clock, default);
        Assert.Equal(TimeSpan.FromSeconds(50), deadline.ProviderBudget);
        clock.Advance(TimeSpan.FromSeconds(55)); Assert.Equal(TimeSpan.FromSeconds(2), deadline.ProviderBudget);
        clock.Advance(TimeSpan.FromSeconds(4)); Assert.Throws<ContentReviewTimeoutException>(deadline.Check);
        Assert.Equal(TimeSpan.FromSeconds(60), ContentReviewDeadline.TotalLimit);
    }
    [Fact] public void Client_cancellation_is_distinct()
    {
        using var client = new CancellationTokenSource(); using var deadline = new ContentReviewDeadline(new ReviewClock(), client.Token);
        client.Cancel(); Assert.Throws<OperationCanceledException>(deadline.Check); Assert.True(deadline.Token.IsCancellationRequested);
    }
}
