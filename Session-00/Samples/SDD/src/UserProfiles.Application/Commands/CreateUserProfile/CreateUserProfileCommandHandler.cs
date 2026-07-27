using MediatR;
using UserProfiles.Application.Abstractions;
using UserProfiles.Application.Exceptions;
using UserProfiles.Application.Models;
using UserProfiles.Domain;

namespace UserProfiles.Application.Commands.CreateUserProfile;

public sealed class CreateUserProfileCommandHandler(
    IUserProfileRepository repository,
    IClock clock) : IRequestHandler<CreateUserProfileCommand, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(
        CreateUserProfileCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await repository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new DuplicateEmailException(normalizedEmail);
        }

        var now = clock.UtcNow;
        var profile = UserProfile.Create(
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.DateOfBirth,
            request.Country,
            DateOnly.FromDateTime(now.UtcDateTime),
            now);

        await repository.AddAsync(profile, cancellationToken);
        return UserProfileDto.From(profile);
    }
}

