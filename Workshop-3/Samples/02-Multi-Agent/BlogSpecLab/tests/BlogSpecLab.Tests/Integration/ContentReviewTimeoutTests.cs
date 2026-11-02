using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Endpoints.Api.Http;
using BlogSpecLab.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
namespace BlogSpecLab.Tests.Integration;
public class ContentReviewTimeoutTests
{
    [Theory] [InlineData(ContentReviewOutcome.Candidate)] [InlineData(ContentReviewOutcome.Refused)]
    public async Task Late_provider_cannot_deliver_report(ContentReviewOutcome outcome)
    {
        var clock = new ReviewClock(); await using var f = new ContentReviewApiFactory { Clock = clock };
        var p = await f.Prepare(); f.Gateway.Pause = true; f.Gateway.IgnoreCancellation = true;
        f.Gateway.Result = f.Gateway.Result with { Outcome = outcome };
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]); var pending = c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken);
        await f.Gateway.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken); clock.Advance(TimeSpan.FromSeconds(59));
        var response = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); Assert.Equal(504, (int)response.StatusCode);
        Assert.Equal("ai_review_timeout", (await ContentReviewApiTests.Json(response)).GetProperty("code").GetString());
        f.Gateway.Release.TrySetResult(); f.Gateway.Pause = false;
        Assert.Equal(200, (int)(await c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken)).StatusCode);
    }
    [Theory] [InlineData("authentication")] [InlineData("storage")]
    public async Task Noncooperating_io_keeps_its_scope_until_task_finishes(string stage)
    {
        var clock = new ReviewClock(); var blocked = new BlockedDependency();
        await using var f = new ContentReviewApiFactory { Clock = clock };
        f.OverrideServices = services => {
            if (stage == "authentication") { services.RemoveAll<ICredentialVerifier>(); services.AddScoped<ICredentialVerifier>(_ => blocked); }
            else { services.RemoveAll<IPostContentReviewReader>(); services.AddScoped<IPostContentReviewReader>(_ => blocked); }
        };
        var p = await f.Prepare(); using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]);
        var pending = c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken);
        await blocked.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken); clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(504, (int)(await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).StatusCode);
        Assert.False(blocked.Disposed); blocked.Release.TrySetResult();
        await blocked.Disposal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); Assert.Equal(0, f.Gateway.Calls);
    }
    private sealed class BlockedDependency : ICredentialVerifier, IPostContentReviewReader, IDisposable
    {
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed;
        public async Task<OperationResult<string>> VerifyAsync(string token, CancellationToken cancellationToken = default)
        { Entered.TrySetResult(); await Release.Task; throw new StorageUnavailableException(); }
        public async Task<PostContentReviewProjection?> ReadAsync(Guid id, CancellationToken cancellationToken)
        { Entered.TrySetResult(); await Release.Task; throw new StorageUnavailableException(); }
        public void Dispose() { Disposed = true; Disposal.TrySetResult(); }
    }
    [Fact] public async Task Client_cancellation_reaches_provider()
    {
        await using var f = new ContentReviewApiFactory(); var p = await f.Prepare(); f.Gateway.Pause = true;
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]); using var cancel = new CancellationTokenSource();
        var task = c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, cancel.Token);
        await f.Gateway.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task); f.Gateway.Release.TrySetResult();
    }
}
