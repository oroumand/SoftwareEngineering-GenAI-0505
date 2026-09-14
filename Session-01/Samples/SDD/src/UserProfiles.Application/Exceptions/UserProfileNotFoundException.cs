namespace UserProfiles.Application.Exceptions;

public sealed class UserProfileNotFoundException : Exception
{
    public UserProfileNotFoundException(Guid id)
        : base($"User profile '{id}' was not found.")
    {
    }
}

