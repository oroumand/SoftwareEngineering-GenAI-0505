using MediatR;
using UserProfiles.Application.Models;

namespace UserProfiles.Application.Commands.CreateUserProfile;

public sealed record CreateUserProfileCommand(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string Country) : IRequest<UserProfileDto>;

