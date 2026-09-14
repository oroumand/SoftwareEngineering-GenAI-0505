using MediatR;
using UserProfiles.Api;
using UserProfiles.Api.Endpoints;
using UserProfiles.Application.Commands.CreateUserProfile;
using UserProfiles.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssemblyContaining<CreateUserProfileCommand>());
builder.Services.AddUserProfilesInfrastructure();

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.MapUserProfileEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

public partial class Program;

