using System.Security.Claims;
using System.Text.Json;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Endpoints.Api.Contracts;
using BlogSpecLab.Endpoints.Api.Http;

namespace BlogSpecLab.Endpoints.Api.Endpoints;

public static class AuthorPostEndpoints
{
    public static void MapAuthorPosts(this WebApplication app)
    {
        var group = app.MapGroup("/api/author/posts").RequireAuthorization();
        group.MapPost("", async (HttpContext context, AuthorPostService service) =>
        {
            var input = await ReadContent(context);
            if (!input.IsSuccess) return ProblemResults.FromFailure(input.Failure!);
            var result = await service.CreateAsync(UserId(context), input.Value!.Title, input.Value.Body, context.RequestAborted);
            return ProblemResults.FromOperation(result, view => View(context, view, created: true));
        }).Accepts<PostContentRequest>("application/json").DocumentPost(mutation: true, success: 201);
        group.MapGet("", async (HttpContext context, AuthorPostService service) =>
            ProblemResults.FromOperation(await service.ListAsync(UserId(context), context.RequestAborted), Results.Ok)).DocumentPost(list: true);
        group.MapGet("/{id:guid}", async (Guid id, HttpContext context, AuthorPostService service) =>
            ProblemResults.FromOperation(await service.GetAsync(UserId(context), id, context.RequestAborted), view => View(context, view))).DocumentPost();
        group.MapPut("/{id:guid}", async (Guid id, HttpContext context, AuthorPostService service) =>
        {
            var version = VersionHeaders.ParseIfMatch(context.Request.Headers);
            if (!version.IsSuccess) return ProblemResults.FromFailure(version.Failure!);
            var input = await ReadContent(context);
            if (!input.IsSuccess) return ProblemResults.FromFailure(input.Failure!);
            return ProblemResults.FromOperation(await service.EditAsync(UserId(context), id, version.Value,
                input.Value!.Title, input.Value.Body, context.RequestAborted), view => View(context, view));
        }).Accepts<PostContentRequest>("application/json").DocumentPost(mutation: true);
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext context, AuthorPostService service) =>
        {
            var version = VersionHeaders.ParseIfMatch(context.Request.Headers);
            if (!version.IsSuccess) return ProblemResults.FromFailure(version.Failure!);
            if (await HasBody(context)) return ProblemResults.FromFailure(new(OperationError.InvalidInput));
            return ProblemResults.FromOperation(await service.DeleteAsync(UserId(context), id, version.Value, context.RequestAborted), _ => Results.NoContent());
        }).DocumentPost(mutation: true, success: 204);
        group.MapPost("/{id:guid}/submit", async (Guid id, HttpContext context, AuthorPostService service) =>
        {
            var version = VersionHeaders.ParseIfMatch(context.Request.Headers);
            if (!version.IsSuccess) return ProblemResults.FromFailure(version.Failure!);
            if (await HasBody(context)) return ProblemResults.FromFailure(new(OperationError.InvalidInput));
            return ProblemResults.FromOperation(await service.SubmitAsync(UserId(context), id, version.Value, context.RequestAborted), view => View(context, view));
        }).DocumentPost(mutation: true);
    }
    private static string? UserId(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    private static IResult View(HttpContext context, PostView view, bool created = false)
    {
        context.Response.Headers.ETag = VersionHeaders.Format(view.Version);
        return created ? Results.Created($"/api/author/posts/{view.Id:D}", view) : Results.Ok(view);
    }
    private static async Task<OperationResult<PostContentRequest>> ReadContent(HttpContext context)
    {
        if (!context.Request.HasJsonContentType()) return OperationResult<PostContentRequest>.Fail(OperationError.InvalidInput);
        try
        {
            var input = await context.Request.ReadFromJsonAsync<PostContentRequest>(context.RequestAborted);
            return input is null ? OperationResult<PostContentRequest>.Fail(OperationError.InvalidInput) : OperationResult<PostContentRequest>.Success(input);
        }
        catch (JsonException) { return OperationResult<PostContentRequest>.Fail(OperationError.InvalidInput); }
        catch (BadHttpRequestException) { return OperationResult<PostContentRequest>.Fail(OperationError.InvalidInput); }
    }
    private static async Task<bool> HasBody(HttpContext context)
    {
        var buffer = new byte[1];
        return await context.Request.Body.ReadAsync(buffer, context.RequestAborted) != 0;
    }
}

