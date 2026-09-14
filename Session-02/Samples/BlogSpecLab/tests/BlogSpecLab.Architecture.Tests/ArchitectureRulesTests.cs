using BlogSpecLab.Api.Posts.Controllers;
using BlogSpecLab.Domain.Posts.Entities;

namespace BlogSpecLab.Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    [Fact]
    public void ProductionProjects_RespectTheOnionReferenceDirection()
    {
        var domainReferences = typeof(Post).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
        var applicationReferences = typeof(BlogSpecLab.Application.Posts.Ports.PostVersion).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
        var infrastructureReferences = typeof(BlogSpecLab.Infrastructure.Posts.Persistence.BlogSpecLabDbContext).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
        var apiReferences = typeof(PostsController).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();

        Assert.DoesNotContain(domainReferences, name => name!.StartsWith("BlogSpecLab.", StringComparison.Ordinal));
        Assert.DoesNotContain(domainReferences, name => name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(applicationReferences, name => name is "BlogSpecLab.Infrastructure" or "BlogSpecLab.Api");
        Assert.Contains("BlogSpecLab.Domain", applicationReferences);
        Assert.DoesNotContain(infrastructureReferences, name => name == "BlogSpecLab.Api");
        Assert.Contains("BlogSpecLab.Application", infrastructureReferences);
        Assert.Contains("BlogSpecLab.Domain", infrastructureReferences);
        Assert.Contains("BlogSpecLab.Infrastructure", apiReferences);
    }

    [Fact]
    public void PostsArtifacts_AreOrganizedUnderTheirAggregateNamespace()
    {
        var assemblies = new[]
        {
            typeof(Post).Assembly,
            typeof(BlogSpecLab.Application.Posts.Ports.PostVersion).Assembly,
            typeof(BlogSpecLab.Infrastructure.Posts.Persistence.BlogSpecLabDbContext).Assembly,
            typeof(PostsController).Assembly
        };

        foreach (var type in assemblies.SelectMany(item => item.GetTypes())
                     .Where(item => item.Name.Contains("Post", StringComparison.Ordinal) && item.Namespace != "BlogSpecLab.Infrastructure.Migrations"))
        {
            Assert.Contains(".Posts.", type.Namespace ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Controller_DoesNotReceiveInfrastructureConcreteTypes()
    {
        var infrastructureAssembly = typeof(BlogSpecLab.Infrastructure.Posts.Persistence.BlogSpecLabDbContext).Assembly;
        var constructorParameters = typeof(PostsController).GetConstructors()
            .SelectMany(constructor => constructor.GetParameters());

        Assert.DoesNotContain(constructorParameters, parameter => parameter.ParameterType.Assembly == infrastructureAssembly);
    }
}
