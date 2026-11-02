using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace BlogSpecLab.Tests.Integration;
public class ContentReviewElapsedTests
{
    [Fact] public async Task Connected_client_gets_timeout_before_sixty_seconds()
    {
        var serverTiming = new ServerTiming();
        await using var f = new ContentReviewApiFactory(); f.OverrideServices = s => s.AddSingleton<IStartupFilter>(serverTiming);
        var p = await f.Prepare(); f.Gateway.Pause = true; f.Gateway.IgnoreCancellation = true;
        using var c = f.CreateAuthenticatedClient(f.Users.Authors[0]); c.Timeout = TimeSpan.FromSeconds(70);
        try {
            var response = await c.PostAsync(ContentReviewApiFactory.Route(p.Id), null, TestContext.Current.CancellationToken);
            var elapsed = await serverTiming.Prepared.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(504, (int)response.StatusCode); Assert.True(elapsed <= TimeSpan.FromSeconds(60));
        } finally { f.Gateway.Release.TrySetResult(); }
    }
    private sealed class ServerTiming : IStartupFilter
    {
        public TaskCompletionSource<TimeSpan> Prepared { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app => {
            app.Use(async (context, continuation) => {
                if (!context.Request.Path.Value!.EndsWith("/content-review")) { await continuation(context); return; }
                var stopwatch = Stopwatch.StartNew(); await continuation(context);
                Prepared.TrySetResult(stopwatch.Elapsed);
            });
            next(app);
        };
    }
}
