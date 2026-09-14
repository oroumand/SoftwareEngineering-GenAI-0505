namespace BlogSpecLab.Api.Posts.Models;

public sealed class WritePostRequest
{
    public string? Title { get; init; }

    public string? Content { get; init; }
}
