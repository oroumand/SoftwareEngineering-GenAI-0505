using MediatR;
using UserProfiles.Application.Commands.CreateUserProfile;
using UserProfiles.Application.Models;
using UserProfiles.Application.Queries.GetUserProfileById;
using UserProfiles.Application.Queries.ListUserProfiles;

namespace UserProfiles.Api.Endpoints;

public static class UserProfileEndpoints
{
    public static IEndpointRouteBuilder MapUserProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users").WithTags("User Profiles");

        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetByIdAsync).WithName("GetUserProfileById");

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateUserProfileRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateUserProfileCommand(
                request.FirstName,
                request.LastName,
                request.Email,
                request.PhoneNumber,
                request.DateOfBirth,
                request.Country),
            cancellationToken);

        return TypedResults.CreatedAtRoute(
            result,
            "GetUserProfileById",
            new { id = result.Id });
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUserProfileByIdQuery(id), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListUserProfilesQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }
}

public sealed record CreateUserProfileRequest(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string Country);

