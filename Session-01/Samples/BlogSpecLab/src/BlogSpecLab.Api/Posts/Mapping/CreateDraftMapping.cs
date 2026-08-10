using BlogSpecLab.Api.Posts.Models;
using BlogSpecLab.Application.Posts.CreateDraft;

namespace BlogSpecLab.Api.Posts.Mapping;

public static class CreateDraftMapping
{
    public static DraftCreatedResponse ToResponse(CreateDraftResult result) => new(result.Id.Value, "Draft");
}
