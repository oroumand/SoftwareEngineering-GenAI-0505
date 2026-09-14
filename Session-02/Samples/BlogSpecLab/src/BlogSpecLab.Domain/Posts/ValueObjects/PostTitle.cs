using BlogSpecLab.Domain.Posts.Errors;

namespace BlogSpecLab.Domain.Posts.ValueObjects;

public sealed record PostTitle
{
    public PostTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PostValidationException("عنوان پست الزامی است.");
        }

        Value = value;
    }

    public string Value { get; }
}
