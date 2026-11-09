using System.Security.Claims;
using System.Text.Json;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Endpoints.Api.Contracts;
using BlogSpecLab.Endpoints.Api.Http;
namespace BlogSpecLab.Endpoints.Api.Endpoints;

public static class EditorPostEndpoints
{
    public static void MapEditorPosts(this WebApplication app)
    {
        var group = app.MapGroup("/api/editor/posts").RequireAuthorization();
        group.MapGet("", async (HttpContext context, EditorPostService service) =>
            ProblemResults.FromOperation(await service.ListAsync(UserId(context), context.RequestAborted), Results.Ok)).DocumentPost(list: true);
        group.MapGet("/{id:guid}", async (Guid id, HttpContext context, EditorPostService service) =>
            ProblemResults.FromOperation(await service.GetAsync(UserId(context), id, context.RequestAborted), view => View(context, view))).DocumentPost();
        group.MapPost("/{id:guid}/request-changes", async (Guid id, HttpContext context, EditorPostService service) =>
        {
            var version = VersionHeaders.ParseIfMatch(context.Request.Headers);
            if (!version.IsSuccess) return ProblemResults.FromFailure(version.Failure!);
            var input = await ReadReason(context);
            if (!input.IsSuccess) return ProblemResults.FromFailure(input.Failure!);
            return ProblemResults.FromOperation(await service.RequestChangesAsync(UserId(context), id, version.Value,
                input.Value!.Reason, context.RequestAborted), view => View(context, view));
        }).Accepts<RequestChangesRequest>("application/json").DocumentPost(mutation: true);
        group.MapPost("/{id:guid}/approve", async (Guid id, HttpContext context, EditorPostService service) =>
        {
            var version = VersionHeaders.ParseIfMatch(context.Request.Headers);
            if (!version.IsSuccess) return ProblemResults.FromFailure(version.Failure!);
            if (await HasBody(context)) return ProblemResults.FromFailure(new(OperationError.InvalidInput));
            return ProblemResults.FromOperation(await service.ApproveAsync(UserId(context), id, version.Value,
                context.RequestAborted), view => View(context, view));
        }).DocumentPost(mutation: true);
        group.MapPost("/{id:guid}/publish", async (Guid id, HttpContext context, EditorPostService service) =>
        {
            var version = VersionHeaders.ParseIfMatch(context.Request.Headers);
            if (!version.IsSuccess) return ProblemResults.FromFailure(version.Failure!);
            if (await HasBody(context)) return ProblemResults.FromFailure(new(OperationError.InvalidInput));
            return ProblemResults.FromOperation(await service.PublishAsync(UserId(context), id, version.Value,
                context.RequestAborted), view => View(context, view));
        }).DocumentPost(mutation: true);
    }
    private static string? UserId(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    private static IResult View(HttpContext context, PostView view)
    { context.Response.Headers.ETag = VersionHeaders.Format(view.Version); return Results.Ok(view); }
    private static async Task<OperationResult<RequestChangesRequest>> ReadReason(HttpContext context)
    {
        if (!context.Request.HasJsonContentType()) return OperationResult<RequestChangesRequest>.Fail(OperationError.InvalidInput);
        try
        {
            var input = await context.Request.ReadFromJsonAsync<RequestChangesRequest>(context.RequestAborted);
            return input is null ? OperationResult<RequestChangesRequest>.Fail(OperationError.InvalidInput) : OperationResult<RequestChangesRequest>.Success(input);
        }
        catch (JsonException) { return OperationResult<RequestChangesRequest>.Fail(OperationError.InvalidInput); }
        catch (BadHttpRequestException) { return OperationResult<RequestChangesRequest>.Fail(OperationError.InvalidInput); }
    }
    private static async Task<bool> HasBody(HttpContext context)
    { var buffer = new byte[1]; return await context.Request.Body.ReadAsync(buffer, context.RequestAborted) != 0; }
}

