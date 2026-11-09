using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Core.Domain.Posts;
namespace BlogSpecLab.Core.Application.ContentReview;

public sealed class PostContentReviewService(IPostContentReviewReader reader, IUserAccessReader accessReader,
    IContentReviewGateway gateway, ContentReviewReportValidator validator, TimeProvider clock)
{
    public async Task<OperationResult<ContentReviewEnvelope>> ReviewAsync(string? userId, Guid id, ContentReviewDeadline deadline)
    {
        try
        {
            deadline.Check();
            var denied = await Access(userId, deadline);
            if (denied is not null) return Fail(denied.Value);
            var post = await Read(id, deadline);
            if (!Owned(post, userId!)) return Fail(OperationError.PostNotFound);
            if (post!.Status is not (PostStatus.Draft or PostStatus.ChangesRequested)) return Fail(OperationError.InvalidState);
            if (string.IsNullOrWhiteSpace(post.Title) || string.IsNullOrWhiteSpace(post.Body)) return Fail(OperationError.InvalidInput);
            var snapshot = new ContentReviewSnapshot(id, post.OwnerId, post.Title, post.Body, post.ContentRevision, post.Status, clock.GetUtcNow());
            deadline.Check();
            var budget = deadline.ProviderBudget;
            if (budget <= TimeSpan.Zero) return Fail(OperationError.AiReviewTimeout);
            using var providerTimer = new CancellationTokenSource(budget, clock);
            using var providerToken = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, providerTimer.Token);
            ContentReviewGatewayResult candidate;
            var providerTask = gateway.ReviewAsync(new(snapshot.Title, snapshot.Body), providerToken.Token);
            try { candidate = await providerTask.WaitAsync(budget, clock, providerToken.Token); }
            catch (TimeoutException) { Observe(providerTask); candidate = new(ContentReviewOutcome.Timeout); }
            catch (OperationCanceledException) when (!deadline.Token.IsCancellationRequested && providerTimer.IsCancellationRequested)
            { Observe(providerTask); candidate = new(ContentReviewOutcome.Timeout); }
            catch (OperationCanceledException) { Observe(providerTask); throw; }
            deadline.Check();
            var valid = candidate.Outcome == ContentReviewOutcome.Candidate && validator.IsValid(candidate.Report, snapshot);
            denied = await Access(userId, deadline);
            if (denied is not null) return Fail(denied.Value);
            var current = await Read(id, deadline);
            if (!Owned(current, userId!)) return Fail(OperationError.PostNotFound);
            deadline.Check();
            if (!valid) return Fail(candidate.Outcome switch {
                ContentReviewOutcome.Refused => OperationError.AiReviewRefused,
                ContentReviewOutcome.Unavailable => OperationError.AiReviewUnavailable,
                ContentReviewOutcome.Timeout => OperationError.AiReviewTimeout, _ => OperationError.AiReviewInvalidOutput });
            var fresh = current!.ContentRevision == snapshot.ContentRevision && current.Title == snapshot.Title && current.Body == snapshot.Body;
            return OperationResult<ContentReviewEnvelope>.Success(new(id, new(snapshot.ContentRevision, snapshot.CapturedAt),
                new(clock.GetUtcNow(), fresh), candidate.Report!));
        }
        catch (StorageUnavailableException) { deadline.Check(); return Fail(OperationError.StorageUnavailable); }
        catch (ContentReviewTimeoutException) { return Fail(OperationError.AiReviewTimeout); }
    }
    private async Task<OperationError?> Access(string? userId, ContentReviewDeadline deadline)
    {
        deadline.Check(); if (string.IsNullOrWhiteSpace(userId)) return OperationError.Unauthenticated;
        var access = await accessReader.ReadAsync(userId, deadline.Token); deadline.Check();
        return access is { IsActive: true, CanWrite: true } && access.UserId == userId ? null : OperationError.Forbidden;
    }
    private async Task<PostContentReviewProjection?> Read(Guid id, ContentReviewDeadline deadline)
    { deadline.Check(); var post = await reader.ReadAsync(id, deadline.Token); deadline.Check(); return post; }
    private static bool Owned(PostContentReviewProjection? post, string userId) => post is { IsDeleted: false } && post.OwnerId == userId;
    private static OperationResult<ContentReviewEnvelope> Fail(OperationError error) => OperationResult<ContentReviewEnvelope>.Fail(error);
    private static void Observe(Task task) => _ = task.ContinueWith(t => _ = t.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
