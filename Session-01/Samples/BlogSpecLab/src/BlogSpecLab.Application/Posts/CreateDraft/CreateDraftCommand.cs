namespace BlogSpecLab.Application.Posts.CreateDraft;

public sealed record CreateDraftCommand(string AuthorId, string Title, string Content);
