using MediatR;
using UserProfiles.Application.Abstractions;
using UserProfiles.Application.Models;

namespace UserProfiles.Application.Queries.ListUserProfiles;

public sealed class ListUserProfilesQueryHandler(IUserProfileRepository repository)
    : IRequestHandler<ListUserProfilesQuery, IReadOnlyList<UserProfileDto>>
{
    public async Task<IReadOnlyList<UserProfileDto>> Handle(
        ListUserProfilesQuery request,
        CancellationToken cancellationToken)
    {
        var profiles = await repository.ListAsync(cancellationToken);
        return profiles.Select(UserProfileDto.From).ToArray();
    }
}

