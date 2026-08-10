namespace BlogSpecLab.Domain.Posts.ValueObjects;

public readonly record struct PostId(Guid Value)
{
    public static PostId New() => new(Guid.NewGuid());
}
