using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Security;
using BlogSpecLab.Endpoints.Api.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BlogSpecLab.Endpoints.Api.Security;

public sealed class OpaqueBearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, ICredentialVerifier verifier)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "OpaqueBearer";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization")) return AuthenticateResult.NoResult();
        if (Request.Headers.Authorization.Count != 1
            || !AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var header)
            || !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
            return AuthenticateResult.Fail("هویت معتبر نیست.");
        var result = await verifier.VerifyAsync(header.Parameter, Context.RequestAborted);
        if (result.Failure?.Error == OperationError.StorageUnavailable) throw new StorageUnavailableException();
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Value))
            return AuthenticateResult.Fail("هویت معتبر نیست.");
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, result.Value)], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        ProblemResults.FromFailure(new(OperationError.Unauthenticated)).ExecuteAsync(Context);

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ProblemResults.FromFailure(new(OperationError.Forbidden)).ExecuteAsync(Context);
}
