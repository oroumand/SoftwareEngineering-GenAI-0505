namespace BlogSpecLab.Core.Application.ContentReview;

public sealed class ContentReviewDeadline : IDisposable
{
    public static readonly TimeSpan WorkLimit = TimeSpan.FromSeconds(59);
    public static readonly TimeSpan TotalLimit = TimeSpan.FromSeconds(60);
    private readonly TimeProvider clock;
    private readonly long started;
    private readonly CancellationToken client;
    private readonly CancellationTokenSource timer;
    private readonly CancellationTokenSource linked;
    public ContentReviewDeadline(TimeProvider clock, CancellationToken client)
    {
        this.clock = clock; this.client = client; started = clock.GetTimestamp();
        timer = new(WorkLimit, clock);
        linked = CancellationTokenSource.CreateLinkedTokenSource(client, timer.Token);
    }
    public CancellationToken Token => linked.Token;
    public TimeSpan Elapsed => clock.GetElapsedTime(started);
    public TimeSpan Remaining => WorkLimit - Elapsed;
    public TimeSpan ProviderBudget => TimeSpan.FromTicks(Math.Max(0,
        Math.Min(TimeSpan.FromSeconds(50).Ticks, (Remaining - TimeSpan.FromSeconds(2)).Ticks)));
    public void Check()
    {
        client.ThrowIfCancellationRequested();
        if (Remaining <= TimeSpan.Zero || timer.IsCancellationRequested) throw new ContentReviewTimeoutException();
    }
    public void Dispose() { linked.Dispose(); timer.Dispose(); }
}
public sealed class ContentReviewTimeoutException : OperationCanceledException;
