using MediatR;
using UserProfiles.Application.Models;

namespace UserProfiles.Application.Queries.GetUserProfileById;

public sealed record GetUserProfileByIdQuery(Guid Id) : IRequest<UserProfileDto>;

