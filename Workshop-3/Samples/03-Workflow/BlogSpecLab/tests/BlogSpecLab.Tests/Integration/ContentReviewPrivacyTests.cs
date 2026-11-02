using System.Collections.Concurrent;
using System.Diagnostics;
using BlogSpecLab.Core.Application.ContentReview;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
namespace BlogSpecLab.Tests.Integration;
public class ContentReviewPrivacyTests
{
    [Theory] [InlineData(ContentReviewOutcome.Candidate)] [InlineData(ContentReviewOutcome.Refused)]
    [InlineData(ContentReviewOutcome.InvalidOutput)] [InlineData(ContentReviewOutcome.Unavailable)]
    public async Task Logs_traces_and_problems_exclude_private_data(ContentReviewOutcome outcome)
    {
        var logs = new LogCapture(); var traces = new ConcurrentQueue<string>();
        using var listener = new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => traces.Enqueue(string.Join(" ", activity.TagObjects.Select(t => $"{t.Key}={t.Value}"))) };
        ActivitySource.AddActivityListener(listener);
        await using var f = new ContentReviewApiFactory(); f.OverrideServices = s => s.AddSingleton<ILoggerProvider>(logs);
        var p = await f.Prepare(); await f.Change(p.Id, post => post.Edit("PRIVATE-TITLE-123", "PRIVATE-BODY-456"));
        f.Gateway.Result = f.Gateway.Result with { Outcome = outcome };
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]); var response = await c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken);
        var captured = string.Join("\n", logs.Messages.Concat(traces));
        if ((int)response.StatusCode != 200) captured += await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        foreach (var privateValue in new[] { "PRIVATE-TITLE-123", "PRIVATE-BODY-456", f.Users.Authors[0].Credential, f.Users.Authors[0].UserId }) Assert.DoesNotContain(privateValue, captured);
        using var other = f.CreateAuthenticatedClient(f.Users.Authors[1]);
        var denied = await other.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken);
        var absent = await other.PostAsync(ContentReviewApiFactory.Route(Guid.NewGuid()), null, TestContext.Current.CancellationToken);
        Assert.Equal(404, (int)denied.StatusCode); Assert.Equal(404, (int)absent.StatusCode);
        Assert.Equal((await ContentReviewApiTests.Json(denied)).GetProperty("detail").GetString(), (await ContentReviewApiTests.Json(absent)).GetProperty("detail").GetString());
    }
    private sealed class LogCapture : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string name) => new Capture(Messages);
        public void Dispose() { }
        private sealed class Capture(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, error));
        }
    }
}
