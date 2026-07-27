using UserProfiles.Domain;

namespace UserProfiles.Application.Models;

public sealed record UserProfileDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string Country,
    DateTimeOffset CreatedAtUtc)
{
    public static UserProfileDto From(UserProfile profile) =>
        new(
            profile.Id,
            profile.FirstName,
            profile.LastName,
            profile.Email,
            profile.PhoneNumber,
            profile.DateOfBirth,
            profile.Country,
            profile.CreatedAtUtc);
}

