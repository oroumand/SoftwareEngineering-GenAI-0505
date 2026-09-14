using MediatR;
using UserProfiles.Application.Abstractions;
using UserProfiles.Application.Exceptions;
using UserProfiles.Application.Models;

namespace UserProfiles.Application.Queries.GetUserProfileById;

public sealed class GetUserProfileByIdQueryHandler(IUserProfileRepository repository)
    : IRequestHandler<GetUserProfileByIdQuery, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(
        GetUserProfileByIdQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new UserProfileNotFoundException(request.Id);

        return UserProfileDto.From(profile);
    }
}

