using BlogSpecLab.Core.Application.Common;
using Microsoft.Extensions.Primitives;

namespace BlogSpecLab.Endpoints.Api.Http;

public static class VersionHeaders
{
    public static string Format(Guid version) => $"\"{version:D}\"";

    public static OperationResult<Guid> ParseIfMatch(IHeaderDictionary headers)
    {
        if (!headers.TryGetValue("If-Match", out StringValues values))
            return OperationResult<Guid>.Fail(OperationError.VersionRequired);
        var text = values.Count == 1 ? values[0]?.Trim() : null;
        if (text is null || text.Length != 38 || text[0] != '"' || text[^1] != '"'
            || !Guid.TryParseExact(text[1..^1], "D", out var version))
            return OperationResult<Guid>.Fail(OperationError.InvalidInput);
        return OperationResult<Guid>.Success(version);
    }
}
