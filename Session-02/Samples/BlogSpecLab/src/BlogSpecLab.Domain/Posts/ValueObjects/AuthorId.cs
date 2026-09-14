using BlogSpecLab.Domain.Posts.Errors;

namespace BlogSpecLab.Domain.Posts.ValueObjects;

public sealed record AuthorId
{
    public AuthorId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PostValidationException("شناسهٔ نویسنده الزامی است.");
        }

        Value = value;
    }

    public string Value { get; }
}
