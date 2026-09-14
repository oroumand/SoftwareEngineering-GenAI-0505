namespace BlogSpecLab.Application.Posts.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
