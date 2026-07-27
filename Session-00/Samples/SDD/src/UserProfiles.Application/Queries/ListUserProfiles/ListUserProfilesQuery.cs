using MediatR;
using UserProfiles.Application.Models;

namespace UserProfiles.Application.Queries.ListUserProfiles;

public sealed record ListUserProfilesQuery : IRequest<IReadOnlyList<UserProfileDto>>;

