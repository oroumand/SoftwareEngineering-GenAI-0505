using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Core.Application.Security;
namespace BlogSpecLab.Endpoints.Api.Http;

public sealed class ContentReviewRequestCoordinator(IServiceScopeFactory scopes, TimeProvider clock)
{
    public Task<OperationResult<ContentReviewEnvelope>> ReviewAsync(string userId, Guid id, ContentReviewDeadline deadline) =>
        RunAsync(sp => sp.GetRequiredService<PostContentReviewService>().ReviewAsync(userId, id, deadline), deadline);
    public Task<OperationResult<string>> AuthenticateAsync(string credential, ContentReviewDeadline deadline) =>
        RunAsync(sp => sp.GetRequiredService<ICredentialVerifier>().VerifyAsync(credential, deadline.Token), deadline);
    private async Task<OperationResult<T>> RunAsync<T>(Func<IServiceProvider, Task<OperationResult<T>>> execute, ContentReviewDeadline deadline)
    {
        deadline.Check();
        // scope متعلق به task است؛ timeout فقط انتظار HTTP را قطع می‌کند.
        var operation = OwnedOperation();
        try
        {
            var result = await operation.WaitAsync(deadline.Remaining, clock, deadline.Token);
            deadline.Check(); return result;
        }
        catch (TimeoutException) { Observe(operation); throw new ContentReviewTimeoutException(); }
        catch (OperationCanceledException) { Observe(operation); deadline.Check(); throw; }
        async Task<OperationResult<T>> OwnedOperation()
        {
            await using var scope = scopes.CreateAsyncScope();
            return await execute(scope.ServiceProvider);
        }
    }
    private static void Observe<T>(Task<T> task) => _ = task.ContinueWith(t => _ = t.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
