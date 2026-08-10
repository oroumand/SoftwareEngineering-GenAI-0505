using BlogSpecLab.Application.Posts.Ports;

namespace BlogSpecLab.Infrastructure.Posts.Time;

public sealed class UtcClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
