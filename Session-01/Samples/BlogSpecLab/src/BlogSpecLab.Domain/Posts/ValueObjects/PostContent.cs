using BlogSpecLab.Domain.Posts.Errors;

namespace BlogSpecLab.Domain.Posts.ValueObjects;

public sealed record PostContent
{
    public PostContent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PostValidationException("متن پست الزامی است.");
        }

        Value = value;
    }

    public string Value { get; }
}
