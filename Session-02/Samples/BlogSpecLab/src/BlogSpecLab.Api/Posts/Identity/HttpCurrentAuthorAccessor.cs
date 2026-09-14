using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace BlogSpecLab.Api.Posts.Identity;

public sealed class HttpCurrentAuthorAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentAuthorAccessor
{
    public string? GetCurrentAuthorId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(subject) ? null : subject;
    }
}
