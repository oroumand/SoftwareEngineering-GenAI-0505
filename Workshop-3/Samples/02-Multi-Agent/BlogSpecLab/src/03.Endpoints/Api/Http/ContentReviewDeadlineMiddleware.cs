using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReview;
namespace BlogSpecLab.Endpoints.Api.Http;
public sealed class ContentReviewDeadlineMiddleware(RequestDelegate next)
{
    public static bool IsReview(PathString path) => path.StartsWithSegments("/api/author/posts")
        && path.Value!.EndsWith("/content-review", StringComparison.OrdinalIgnoreCase);
    public async Task InvokeAsync(HttpContext context, TimeProvider clock)
    {
        if (!IsReview(context.Request.Path)) { await next(context); return; }
        using var deadline = new ContentReviewDeadline(clock, context.RequestAborted);
        context.Features.Set(deadline); ProblemResults.NoStore(context);
        try { await next(context); }
        catch (ContentReviewTimeoutException) { await Timeout(); }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { await Timeout(); }
        async Task Timeout()
        {
            if (context.Response.HasStarted) { context.Abort(); return; }
            context.Response.Clear(); await ProblemResults.FromFailure(new(OperationError.AiReviewTimeout)).ExecuteAsync(context);
        }
    }
}
