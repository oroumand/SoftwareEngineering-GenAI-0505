using UserProfiles.Application.Abstractions;

namespace UserProfiles.Tests;

internal sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}

