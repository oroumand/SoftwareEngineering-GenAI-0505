using UserProfiles.Application.Abstractions;

namespace UserProfiles.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

