namespace BlogSpecLab.Api.Posts.Identity;

public interface ICurrentAuthorAccessor
{
    string? GetCurrentAuthorId();
}
