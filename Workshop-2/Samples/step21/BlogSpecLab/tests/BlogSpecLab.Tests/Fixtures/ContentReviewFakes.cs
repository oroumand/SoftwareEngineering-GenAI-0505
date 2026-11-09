using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Tests.Fixtures;

public sealed class ContentReviewFake : IContentReviewGateway, IPostContentReviewReader, IUserAccessReader
{
    public PostContentReviewProjection? Post { get; set; } = new(Guid.NewGuid(), "author", "عنوان", "متن می‌رود", 1, PostStatus.Draft, false);
    public UserAccess? Access { get; set; } = new("author", true, false, false, true);
    public ContentReviewGatewayResult Result { get; set; } = new(ContentReviewOutcome.Candidate, Clean);
    public static ContentReviewReport Clean => new("completed", "مشکل مشخصی شناسایی نشد؛ تأیید انتشار نیست.", [], []);
    public int Calls { get; private set; }
    public int Reads { get; private set; }
    public ContentReviewInput? Input { get; private set; }
    public bool Pause { get; set; }
    public bool IgnoreCancellation { get; set; }
    public Action? BeforeReturn { get; set; }
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<ContentReviewGatewayResult> ReviewAsync(ContentReviewInput input, CancellationToken ct)
    {
        Calls++; Input = input; Entered.TrySetResult();
        if (Pause) await Release.Task.WaitAsync(IgnoreCancellation ? CancellationToken.None : ct);
        BeforeReturn?.Invoke(); return Result;
    }
    public Task<PostContentReviewProjection?> ReadAsync(Guid id, CancellationToken ct) { Reads++; return Task.FromResult(Post); }
    public Task<UserAccess?> ReadAsync(string id, CancellationToken ct) => Task.FromResult(Access);
}
public sealed class ReviewClock : TimeProvider
{
    private long ticks;
    private readonly List<ManualTimer> timers = [];
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref ticks);
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (timers) { var timer = new ManualTimer(this, callback, state, dueTime, period); timers.Add(timer); return timer; }
    }
    public void Advance(TimeSpan elapsed)
    {
        Interlocked.Add(ref ticks, elapsed.Ticks);
        ManualTimer[] current; lock (timers) current = timers.ToArray();
        foreach (var timer in current) timer.Fire();
    }
    private sealed class ManualTimer(ReviewClock clock, TimerCallback callback, object? state, TimeSpan due, TimeSpan period) : ITimer
    {
        private long target = due == Timeout.InfiniteTimeSpan ? long.MaxValue : clock.GetTimestamp() + due.Ticks;
        private bool disposed;
        public bool Change(TimeSpan dueTime, TimeSpan newPeriod)
        { period = newPeriod; target = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock.GetTimestamp() + dueTime.Ticks; return !disposed; }
        public void Fire()
        {
            if (disposed || clock.GetTimestamp() < target) return;
            target = period <= TimeSpan.Zero ? long.MaxValue : clock.GetTimestamp() + period.Ticks;
            callback(state);
        }
        public void Dispose() => disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
