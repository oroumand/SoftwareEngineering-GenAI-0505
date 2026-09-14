using BlogSpecLab.Api.Posts.Models;
using BlogSpecLab.Api.Posts.Errors;
using BlogSpecLab.Api.Posts.Identity;
using BlogSpecLab.Api.Posts.Mapping;
using BlogSpecLab.Application.Posts.CreateDraft;
using BlogSpecLab.Application.Posts.Ports;
using Microsoft.AspNetCore.Mvc;

namespace BlogSpecLab.Api.Posts.Controllers;

[ApiController]
[Route("api/posts")]
public sealed class PostsController(ICurrentAuthorAccessor currentAuthorAccessor, CreateDraftHandler createDraftHandler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> CreateDraft([FromBody] WritePostRequest request, CancellationToken cancellationToken)
    {
        var authorId = currentAuthorAccessor.GetCurrentAuthorId();
        if (authorId is null)
        {
            return PostProblemDetails.Create(StatusCodes.Status401Unauthorized, "authentication-required", "هویت نویسنده الزامی است.");
        }

        try
        {
            var result = await createDraftHandler.HandleAsync(
                new CreateDraftCommand(authorId, request.Title ?? string.Empty, request.Content ?? string.Empty),
                cancellationToken);
            Response.Headers.ETag = ETagMapping.ToETag(result.Version);
            return CreatedAtAction(nameof(GetPublished), new { postId = result.Id.Value }, CreateDraftMapping.ToResponse(result));
        }
        catch (ValidationUseCaseException)
        {
            return PostProblemDetails.Create(StatusCodes.Status400BadRequest, "validation-error", "درخواست نامعتبر است.");
        }
    }

    [HttpGet]
    public ActionResult ListPublished() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("{postId:guid}")]
    public ActionResult GetPublished(Guid postId) => PostProblemDetails.Create(StatusCodes.Status404NotFound, "post-not-found", "پست یافت نشد.");

    [HttpPut("{postId:guid}")]
    public ActionResult EditDraft(Guid postId, [FromBody] WritePostRequest request) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("{postId:guid}/publish")]
    public ActionResult PublishDraft(Guid postId) => StatusCode(StatusCodes.Status501NotImplemented);
}
