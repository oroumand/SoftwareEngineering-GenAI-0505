using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Endpoints.Api.Http;
using BlogSpecLab.Endpoints.Api.Security;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Infra.DataAccess.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlogSpecLab.Tests.Integration;

public sealed class FoundationContractTests
{
    [Fact]
    public void InactiveAccessHasNoOperationalPermissions()
    {
        var access = new UserAccess("user", true, true, true, false);
        Assert.False(access.CanWrite || access.CanReview || access.CanPublish);
    }

    [Fact]
    public void CredentialModelUsesUniqueHashAndRestrictsUserDeletion()
    {
        using var db = new BlogDbContext(new DbContextOptionsBuilder<BlogDbContext>().UseSqlite("Data Source=:memory:").Options);
        var entity = db.Model.FindEntityType(typeof(Credential))!;
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(Credential.TokenHash));
        Assert.Equal(DeleteBehavior.Restrict, entity.GetForeignKeys().Single().DeleteBehavior);
        Assert.Null(entity.FindProperty("Token"));
    }

    [Theory]
    [InlineData(null, OperationError.VersionRequired)]
    [InlineData("", OperationError.InvalidInput)]
    [InlineData("*", OperationError.InvalidInput)]
    [InlineData("W/\"00000000-0000-0000-0000-000000000001\"", OperationError.InvalidInput)]
    [InlineData("00000000-0000-0000-0000-000000000001", OperationError.InvalidInput)]
    [InlineData("\"not-a-uuid\"", OperationError.InvalidInput)]
    [InlineData("\"00000000-0000-0000-0000-000000000001\", \"00000000-0000-0000-0000-000000000002\"", OperationError.InvalidInput)]
    public void InvalidVersionHeadersHaveContractErrors(string? text, OperationError expected)
    {
        var headers = new HeaderDictionary();
        if (text is not null) headers["If-Match"] = text;
        Assert.Equal(expected, VersionHeaders.ParseIfMatch(headers).Failure!.Error);
    }

    [Fact]
    public void StrongUuidVersionRoundTripsAndMultipleHeaderLinesAreRejected()
    {
        var version = Guid.NewGuid();
        var headers = new HeaderDictionary { ["If-Match"] = VersionHeaders.Format(version) };
        Assert.Equal(version, VersionHeaders.ParseIfMatch(headers).Value);
        headers.Append("If-Match", VersionHeaders.Format(Guid.NewGuid()));
        Assert.Equal(OperationError.InvalidInput, VersionHeaders.ParseIfMatch(headers).Failure!.Error);
    }

    [Theory]
    [InlineData(OperationError.InvalidInput, 400, "invalid_input")]
    [InlineData(OperationError.Unauthenticated, 401, "unauthenticated")]
    [InlineData(OperationError.Forbidden, 403, "forbidden")]
    [InlineData(OperationError.SelfApprovalForbidden, 403, "self_approval_forbidden")]
    [InlineData(OperationError.PostNotFound, 404, "post_not_found")]
    [InlineData(OperationError.InvalidState, 409, "invalid_state")]
    [InlineData(OperationError.PublishedPostImmutable, 409, "published_post_immutable")]
    [InlineData(OperationError.ApprovalRequired, 409, "approval_required")]
    [InlineData(OperationError.StaleVersion, 412, "stale_version")]
    [InlineData(OperationError.VersionRequired, 428, "version_required")]
    [InlineData(OperationError.StorageUnavailable, 503, "storage_unavailable")]
    [InlineData(OperationError.Unexpected, 500, "unexpected_error")]
    public async Task FailureMappingUsesSafeProblemDetails(OperationError error, int status, string code)
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/contract-test";
        using var body = new MemoryStream();
        context.Response.Body = body;
        var sensitive = Guid.NewGuid().ToString("N");
        await ProblemResults.FromFailure(new(error, new Dictionary<string, string[]> { ["title"] = [sensitive] },
            sensitive, Guid.NewGuid())).ExecuteAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        if (status == 401) Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate.ToString());
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal("about:blank", json.RootElement.GetProperty("type").GetString());
        Assert.Equal("/contract-test", json.RootElement.GetProperty("instance").GetString());
        if (status == 500) Assert.False(json.RootElement.ToString().Contains(sensitive, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealHandlerExtractsOnlyVerifiedIdentityAndIgnoresClientClaims(bool valid)
    {
        // آزمون منفرد handler؛ اتصال verifier واقعی به Schema در T017 بررسی می‌شود.
        using var services = CreateAuthenticationServices(valid);
        var context = new DefaultHttpContext { RequestServices = services };
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        context.Request.Headers.Authorization = "Bearer " + token;
        context.Request.Headers["X-Role"] = "Editor";
        context.Request.Headers["X-OwnerId"] = "forged";
        context.Request.Headers["X-CanPublish"] = "true";
        var result = await context.AuthenticateAsync(OpaqueBearerHandler.SchemeName);
        Assert.Equal(valid, result.Succeeded);
        if (valid)
        {
            var claim = Assert.Single(result.Principal!.Claims);
            Assert.Equal(ClaimTypes.NameIdentifier, claim.Type);
            Assert.Equal("verified-user", claim.Value);
            Assert.False(result.Principal.IsInRole("Editor"));
        }
    }

    [Fact]
    public async Task MissingCredentialChallengesWithBearerAndNoStore()
    {
        using var services = CreateAuthenticationServices(false);
        var context = new DefaultHttpContext { RequestServices = services };
        using var body = new MemoryStream();
        context.Response.Body = body;
        var result = await context.AuthenticateAsync(OpaqueBearerHandler.SchemeName);
        Assert.False(result.Succeeded);
        await context.ChallengeAsync(OpaqueBearerHandler.SchemeName);
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate.ToString());
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    private static ServiceProvider CreateAuthenticationServices(bool valid)
    {
        var services = new ServiceCollection().AddLogging().AddOptions();
        services.AddSingleton(UrlEncoder.Default);
        services.AddSingleton<ICredentialVerifier>(new IsolatedVerifier(valid));
        services.AddAuthentication(OpaqueBearerHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, OpaqueBearerHandler>(OpaqueBearerHandler.SchemeName, _ => { });
        return services.BuildServiceProvider();
    }

    private sealed class IsolatedVerifier(bool valid) : ICredentialVerifier
    {
        public Task<OperationResult<string>> VerifyAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(valid ? OperationResult<string>.Success("verified-user")
                : OperationResult<string>.Fail(OperationError.Unauthenticated));
    }
}
