var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var users = new List<User>();

app.MapPost("/users", (CreateUserRequest request) =>
{
    var user = new User(
        Guid.NewGuid(),
        request.FirstName,
        request.LastName,
        request.Email,
        request.PhoneNumber,
        request.DateOfBirth,
        request.Country,
        DateTimeOffset.UtcNow);

    users.Add(user);
    return Results.Ok(user);
});

app.MapGet("/users", () => Results.Ok(users));

app.Run();

public sealed record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string Country);

public sealed record User(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string Country,
    DateTimeOffset CreatedAtUtc);

