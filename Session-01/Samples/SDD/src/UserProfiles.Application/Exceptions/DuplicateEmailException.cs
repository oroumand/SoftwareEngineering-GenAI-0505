namespace UserProfiles.Application.Exceptions;

public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string email)
        : base($"A user profile with email '{email}' already exists.")
    {
    }
}

