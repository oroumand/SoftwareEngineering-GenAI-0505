using System.Security.Claims;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReview;
using BlogSpecLab.Endpoints.Api.Contracts;
using BlogSpecLab.Endpoints.Api.Http;
namespace BlogSpecLab.Endpoints.Api.Endpoints;
public static class ContentReviewEndpoints
{
    public static void MapContentReview(this WebApplication app)
    {
        var route = app.MapPost("/api/author/posts/{id}/content-review", async (string id, HttpContext context,
            ContentReviewRequestCoordinator coordinator) =>
        {
            var deadline = context.Features.Get<ContentReviewDeadline>()!; deadline.Check();
            if (!Guid.TryParse(id, out var postId)) return ProblemResults.FromFailure(new(OperationError.InvalidInput));
            // حتی body با transfer-encoding و بدون Content-Length رد می‌شود.
            var buffer = new byte[1];
            if (await context.Request.Body.ReadAsync(buffer, deadline.Token) != 0) return ProblemResults.FromFailure(new(OperationError.InvalidInput));
            deadline.Check();
            var result = await coordinator.ReviewAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, postId, deadline);
            deadline.Check();
            return ProblemResults.FromOperation(result, value => Results.Ok(ContentReviewResponse.From(value)));
        }).RequireAuthorization().WithName("ReviewPostContent");
        route.Produces<ContentReviewResponse>();
        foreach (var status in new[] { 400, 401, 403, 404, 409, 422, 502, 503, 504, 500 }) route.ProducesProblem(status);
    }
}
