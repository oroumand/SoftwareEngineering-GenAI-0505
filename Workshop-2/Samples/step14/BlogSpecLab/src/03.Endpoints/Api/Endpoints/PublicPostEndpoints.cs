using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Endpoints.Api.Http;

namespace BlogSpecLab.Endpoints.Api.Endpoints;

public static class PublicPostEndpoints
{
    public static void MapPublicPosts(this WebApplication app)
    {
        var group = app.MapGroup("/api/posts").AllowAnonymous();
        group.MapGet("", async (HttpContext context, PublicPostService service) =>
            ProblemResults.FromOperation(await service.ListAsync(context.RequestAborted), Results.Ok));
        group.MapGet("/{id:guid}", async (Guid id, HttpContext context, PublicPostService service) =>
            ProblemResults.FromOperation(await service.GetAsync(id, context.RequestAborted), Results.Ok));
    }
}
