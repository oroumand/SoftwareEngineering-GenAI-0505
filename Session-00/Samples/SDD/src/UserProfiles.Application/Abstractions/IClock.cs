namespace UserProfiles.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

