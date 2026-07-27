using UserProfiles.Api.Endpoints;
using UserProfiles.Application.Commands.CreateUserProfile;
using UserProfiles.Domain;

namespace UserProfiles.Tests;

public sealed class ContractAndArchitectureTests
{
    [Fact]
    public void CreateRequest_HasExactlySixInputProperties()
    {
        var propertyNames = typeof(CreateUserProfileRequest)
            .GetProperties()
            .Select(property => property.Name)
            .Order()
            .ToArray();

        Assert.Equal(
            [
                "Country",
                "DateOfBirth",
                "Email",
                "FirstName",
                "LastName",
                "PhoneNumber"
            ],
            propertyNames);
    }

    [Fact]
    public void Domain_DoesNotReferenceOtherSolutionLayers()
    {
        var references = typeof(UserProfile).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name?.StartsWith("UserProfiles.", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Empty(references);
    }

    [Fact]
    public void Application_DoesNotReferenceOuterLayers()
    {
        var references = typeof(CreateUserProfileCommand).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("UserProfiles.Infrastructure", references);
        Assert.DoesNotContain("UserProfiles.Api", references);
    }
}

